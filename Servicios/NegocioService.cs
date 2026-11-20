using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using ReposteriaApi.Datos;
using ReposteriaApi.Infra;
using ReposteriaApi.Modelo;

namespace ReposteriaApi.Servicios;

/// <summary>
/// Aquí vive la lógica de los cinco pilares: capacidad multi-recurso, sugeridor de fechas,
/// costeo, rentabilidad con alertas y panel ejecutivo. Es la misma que antes estaba en el
/// DatosService de Angular, ahora en el servidor para que se aplique siempre, sin depender del navegador.
/// </summary>
public class NegocioService(IRepositorio repo, TimeProvider reloj, NlpClient nlp)
{
    private static readonly string[] Meses = ["Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"];

    // ==========================================================================
    // Carga: una "foto" de todos los datos para hacer las cuentas en memoria
    // ==========================================================================

    private sealed class Ctx
    {
        public DateTime Hoy { get; init; }
        public List<Pedido> Pedidos { get; init; } = [];
        public List<Insumo> Insumos { get; init; } = [];
        public List<Receta> Recetas { get; init; } = [];
        public Dictionary<string, decimal> Capacidad { get; init; } = [];
        public List<HistorialMensual> Historial { get; init; } = [];
        public int Interpretaciones { get; init; }

        public Dictionary<int, Insumo> InsumoPorId => Insumos.ToDictionary(i => i.Id);
        public Dictionary<int, Receta> RecetaPorId => Recetas.ToDictionary(r => r.Id);
    }

    private async Task<Ctx> CargarAsync()
    {
        var capacidades = await repo.CapacidadesAsync();
        var capacidad = new Dictionary<string, decimal>(Catalogo.CapacidadPorDefecto);
        foreach (var c in capacidades) capacidad[c.Recurso] = c.HorasDia;

        return new Ctx
        {
            Hoy = reloj.GetLocalNow().DateTime.Date,
            Pedidos = await repo.PedidosAsync(),
            Insumos = await repo.InsumosAsync(),
            Recetas = await repo.RecetasAsync(),
            Capacidad = capacidad,
            Historial = (await repo.HistorialAsync()).OrderBy(h => h.Anio).ThenBy(h => h.Mes).ToList(),
            Interpretaciones = await repo.ContarSolicitudesAsync(),
        };
    }

    // ==========================================================================
    // Pedidos
    // ==========================================================================

    public async Task<List<PedidoDto>> ListarPedidosAsync(string? estado, string? busqueda)
    {
        var c = await CargarAsync();
        var texto = Normalizar(busqueda);
        var recetas = c.RecetaPorId;

        return c.Pedidos
            .Where(p => string.IsNullOrWhiteSpace(estado) || p.Estado == estado)
            .Where(p => texto == "" || Normalizar($"{p.Cliente} {p.Producto}").Contains(texto))
            .OrderBy(p => p.Fecha).ThenBy(p => p.Id)
            .Select(p => ADto(p, recetas))
            .ToList();
    }

    public async Task<PedidoDto> ObtenerPedidoAsync(int id)
    {
        var c = await CargarAsync();
        var pedido = c.Pedidos.FirstOrDefault(p => p.Id == id) ?? throw new NegocioException(404, $"No existe el pedido {id}.");
        return ADto(pedido, c.RecetaPorId);
    }

    public async Task<PedidoDto> CrearPedidoAsync(NuevoPedidoDto dto)
    {
        var c = await CargarAsync();

        var cliente = (dto.Cliente ?? "").Trim();
        if (cliente == "") throw new NegocioException(400, "Falta el nombre del cliente.");
        if (cliente.Length > 120) throw new NegocioException(400, "El nombre del cliente es demasiado largo.");

        var receta = c.Recetas.FirstOrDefault(r => r.Codigo == dto.Receta)
            ?? throw new NegocioException(400, $"No existe la receta '{dto.Receta}'.");
        if (!Catalogo.Complejidades.Contains(dto.Complejidad))
            throw new NegocioException(400, "La complejidad debe ser Baja, Media o Alta.");
        if (dto.Escala <= 0 || dto.Escala > 10) throw new NegocioException(400, "La escala debe estar entre 0 y 10.");

        var fecha = dto.Fecha.Date;
        if (fecha < c.Hoy) throw new NegocioException(400, "La fecha de entrega no puede ser anterior a hoy.");

        if (dto.SolicitudId is int solicitudId && !await repo.ExisteSolicitudAsync(solicitudId))
            throw new NegocioException(400, $"No existe la solicitud {solicitudId}.");

        // El servidor vuelve a verificar la capacidad: evita el doble agendamiento aunque dos personas pidan a la vez
        var faltantes = Verificar(c, UsoDelDia(c, fecha), dto.Complejidad);
        if (faltantes.Count > 0)
        {
            throw new NegocioException(409, "No hay capacidad de producción para esa fecha.", new
            {
                faltantes,
                sugerencia = Sugerir(c, fecha, dto.Complejidad),
            });
        }

        var precio = dto.Precio ?? Cotizar(c, receta, dto.Escala).Precio;
        if (precio <= 0) throw new NegocioException(400, "El precio debe ser mayor que cero.");

        var producto = string.IsNullOrWhiteSpace(dto.Producto) ? "Pedido personalizado" : dto.Producto.Trim();
        var ahora = reloj.GetLocalNow().DateTime;

        var pedido = new Pedido
        {
            Codigo = "TMP-" + Guid.NewGuid().ToString("N")[..8], // provisional: el definitivo usa el Id
            Cliente = cliente,
            Producto = producto.Length > 200 ? producto[..200] : producto,
            RecetaId = receta.Id,
            Complejidad = dto.Complejidad,
            Fecha = fecha,
            Estado = Catalogo.Pendiente, // la repostera todavía tiene que aprobarlo
            Precio = precio,
            Origen = dto.SolicitudId is null ? "Manual" : "IA",
            Escala = dto.Escala,
            CreadoEn = ahora,
            SolicitudId = dto.SolicitudId,
            // La "foto" de precios de hoy: con ella se sabrá después si una alza lo afecta
            PreciosCotizados = c.Insumos.Select(i => new PedidoPrecio { InsumoId = i.Id, Precio = i.PrecioActual ?? i.PrecioCotizado }).ToList(),
        };

        await repo.InsertarAsync([pedido]);
        pedido.Codigo = $"P-{pedido.Id:000}";
        await repo.GuardarAsync();

        return ADto(pedido, c.RecetaPorId);
    }

    public async Task<PedidoDto> CambiarEstadoAsync(int id, string estado)
    {
        if (!Catalogo.Estados.Contains(estado))
            throw new NegocioException(400, $"Estado inválido. Los válidos son: {string.Join(", ", Catalogo.Estados)}.");

        var pedido = await repo.PedidoAsync(id) ?? throw new NegocioException(404, $"No existe el pedido {id}.");
        pedido.Estado = estado;
        await repo.GuardarAsync();

        var recetas = (await repo.RecetasAsync()).ToDictionary(r => r.Id);
        return ADto(pedido, recetas);
    }

    private static PedidoDto ADto(Pedido p, Dictionary<int, Receta> recetas) =>
        new(p.Id, p.Codigo, p.Cliente, p.Producto, recetas[p.RecetaId].Codigo, p.Complejidad,
            p.Fecha, p.Estado, p.Precio, p.Origen, p.Escala);

    private static bool EstaAbierto(Pedido p) => p.Estado != Catalogo.Entregado && p.Estado != Catalogo.Cancelado;

    // ==========================================================================
    // Insumos y recetas
    // ==========================================================================

    public async Task<List<InsumoDto>> ListarInsumosAsync() =>
        (await repo.InsumosAsync()).OrderBy(i => i.Id).Select(ADto).ToList();

    public async Task<InsumoDto> CambiarPrecioInsumoAsync(int id, decimal? precioActual)
    {
        if (precioActual is < 0) throw new NegocioException(400, "El precio no puede ser negativo.");

        var insumos = await repo.InsumosAsync();
        var insumo = insumos.FirstOrDefault(i => i.Id == id) ?? throw new NegocioException(404, $"No existe el insumo {id}.");
        insumo.PrecioActual = precioActual;
        await repo.GuardarAsync();
        return ADto(insumo);
    }

    /// <summary>Vuelve los precios de hoy a los de ejemplo (para repetir la demostración).</summary>
    public async Task<List<InsumoDto>> RestablecerInsumosAsync()
    {
        var insumos = await repo.InsumosAsync();
        foreach (var i in insumos)
            i.PrecioActual = Semilla.PreciosActuales.TryGetValue(i.Codigo, out var precio) ? precio : i.PrecioCotizado;
        await repo.GuardarAsync();
        return insumos.OrderBy(i => i.Id).Select(ADto).ToList();
    }

    public async Task<List<RecetaDto>> ListarRecetasAsync()
    {
        var c = await CargarAsync();
        var insumos = c.InsumoPorId;
        return c.Recetas.OrderBy(r => r.Id)
            .Select(r => new RecetaDto(r.Id, r.Codigo, r.Nombre, r.TamanoBase, r.ManoObra,
                r.Insumos.Select(x => new RecetaInsumoDto(insumos[x.InsumoId].Codigo, x.Cantidad)).ToList()))
            .ToList();
    }

    private static InsumoDto ADto(Insumo i) => new(i.Id, i.Codigo, i.Nombre, i.Unidad, i.PrecioCotizado, i.PrecioActual);

    // ==========================================================================
    // Cotización: del texto de la Captura a una receta, un tamaño y un precio
    // ==========================================================================

    public async Task<CotizacionDto> CotizarAsync(CotizarDto dto)
    {
        var c = await CargarAsync();
        var codigo = RecetaPara(dto.Sabor, dto.Tamano, dto.Diseno, dto.Tipo);
        var receta = c.Recetas.FirstOrDefault(r => r.Codigo == codigo)
            ?? throw new NegocioException(500, $"La receta '{codigo}' no está en la base de datos.");
        var escala = EscalaPara(receta, dto.Tamano);
        var (costo, precio) = Cotizar(c, receta, escala);
        return new CotizacionDto(receta.Codigo, escala, costo, precio, Catalogo.MargenObjetivo);
    }

    /// <summary>Elige la receta según lo que la Captura entendió (sabor, tamaño y diseño).</summary>
    public static string RecetaPara(string? sabor, string? tamano, string? diseno, string? tipo = null)
    {
        var t = Normalizar($"{sabor} {tamano} {diseno}");
        var producto = Normalizar(tipo); // lo que entendió el NLP: torta, cupcakes, postres o boda
        if (producto == "cupcakes" || t.Contains("cupcake")) return "cupcakes";
        if (producto == "boda" || t.Contains("boda")) return "boda";
        if (producto == "postres" || t.Contains("postre")) return "postres";
        if (t.Contains("tres leches") || t.Contains("3 leches")) return "tresleches";

        // Si hay un diseño y no es de los sencillos, es una torta temática (león, unicornio, bautizo...)
        var d = Normalizar(diseno);
        string[] sencillos = ["flores", "minimalista"];
        var hayDiseno = d != "" && d != "no detectado";
        if (hayDiseno && !sencillos.Any(d.Contains)) return "tematica";
        return "clasica";
    }

    /// <summary>Cuántas veces el tamaño de la receta: 40 personas en una receta de 20 = 2.</summary>
    public static decimal EscalaPara(Receta receta, string? tamano)
    {
        var numero = Regex.Match(tamano ?? "", @"\d+");
        if (!numero.Success || !decimal.TryParse(numero.Value, out var cantidad) || cantidad == 0) return 1;
        return Math.Clamp(cantidad / receta.TamanoBase, 0.5m, 4m);
    }

    private static (decimal Costo, decimal Precio) Cotizar(Ctx c, Receta receta, decimal escala)
    {
        var insumos = c.InsumoPorId;
        var costo = CostoReceta(receta, escala, insumos, i => i.PrecioActual ?? 0);
        var precio = Math.Ceiling(costo / (1 - Catalogo.MargenObjetivo) / 1000) * 1000;
        return (costo, precio);
    }

    // ==========================================================================
    // Agenda multi-recurso y sugeridor de fechas
    // ==========================================================================

    public async Task<List<DiaDto>> DiasAsync(int cantidad)
    {
        if (cantidad is < 1 or > 60) throw new NegocioException(400, "La cantidad de días debe estar entre 1 y 60.");
        var c = await CargarAsync();
        return Enumerable.Range(0, cantidad).Select(i => Dia(c, c.Hoy.AddDays(i))).ToList();
    }

    public async Task<VerificacionDto> VerificarAsync(VerificarDto dto)
    {
        if (!Catalogo.Complejidades.Contains(dto.Complejidad))
            throw new NegocioException(400, "La complejidad debe ser Baja, Media o Alta.");

        var c = await CargarAsync();
        var fecha = dto.Fecha.Date;
        if (fecha < c.Hoy) throw new NegocioException(400, "No se puede agendar en una fecha pasada.");

        var faltantes = Verificar(c, UsoDelDia(c, fecha), dto.Complejidad);
        return new VerificacionDto(fecha, faltantes.Count == 0, faltantes,
            faltantes.Count == 0 ? null : Sugerir(c, fecha, dto.Complejidad));
    }

    public async Task<CapacidadDto> ObtenerCapacidadAsync()
    {
        var c = await CargarAsync();
        return new CapacidadDto(c.Capacidad["horno"], c.Capacidad["decoracion"], c.Capacidad["personal"]);
    }

    public async Task<CapacidadDto> ActualizarCapacidadAsync(CapacidadDto dto)
    {
        var nuevas = new Dictionary<string, decimal> { ["horno"] = dto.Horno, ["decoracion"] = dto.Decoracion, ["personal"] = dto.Personal };
        if (nuevas.Values.Any(h => h <= 0 || h > 24))
            throw new NegocioException(400, "Las horas por día de cada recurso deben estar entre 0 y 24.");

        var existentes = await repo.CapacidadesAsync();
        var faltan = new List<object>();
        foreach (var (recurso, horas) in nuevas)
        {
            var fila = existentes.FirstOrDefault(x => x.Recurso == recurso);
            if (fila is null) faltan.Add(new Capacidad { Recurso = recurso, HorasDia = horas });
            else fila.HorasDia = horas;
        }
        if (faltan.Count > 0) await repo.InsertarAsync(faltan);
        await repo.GuardarAsync();
        return dto;
    }

    /// <summary>Las reglas que el frontend necesita para dibujar la agenda: horas por día y horas que gasta cada complejidad.</summary>
    public async Task<ReglasDto> ReglasAsync()
    {
        var c = await CargarAsync();
        var consumo = Catalogo.Consumo.ToDictionary(
            x => x.Key,
            x => new UsoDto(x.Value["horno"], x.Value["decoracion"], x.Value["personal"]));
        return new ReglasDto(new CapacidadDto(c.Capacidad["horno"], c.Capacidad["decoracion"], c.Capacidad["personal"]), consumo);
    }

    private static Dictionary<string, decimal> UsoVacio() => Catalogo.Recursos.ToDictionary(r => r, _ => 0m);

    /// <summary>Las horas de cada recurso que ya consumen los pedidos de ese día (menos los cancelados).</summary>
    private static Dictionary<string, decimal> UsoDelDia(Ctx c, DateTime fecha)
    {
        var total = UsoVacio();
        foreach (var p in c.Pedidos)
        {
            if (p.Estado == Catalogo.Cancelado || p.Fecha.Date != fecha.Date) continue;
            var consumo = Catalogo.Consumo[p.Complejidad];
            foreach (var r in Catalogo.Recursos) total[r] += consumo[r];
        }
        return total;
    }

    private static DiaDto Dia(Ctx c, DateTime fecha)
    {
        var uso = UsoDelDia(c, fecha);
        var ocupacion = (int)Redondear(Catalogo.Recursos.Max(r => uso[r] / c.Capacidad[r]) * 100);
        var estado = ocupacion >= 90 ? "lleno" : ocupacion >= 70 ? "medio" : "libre";
        return new DiaDto(fecha, new UsoDto(uso["horno"], uso["decoracion"], uso["personal"]), ocupacion, estado);
    }

    /// <summary>Qué recursos NO alcanzan para un pedido de esa complejidad. Vacío = cabe.</summary>
    private static List<FaltanteDto> Verificar(Ctx c, Dictionary<string, decimal> uso, string complejidad)
    {
        var faltantes = new List<FaltanteDto>();
        foreach (var r in Catalogo.Recursos)
        {
            var necesita = Catalogo.Consumo[complejidad][r];
            var quedan = c.Capacidad[r] - uso[r];
            if (necesita > quedan) faltantes.Add(new FaltanteDto(r, Catalogo.NombresRecurso[r], necesita, quedan));
        }
        return faltantes;
    }

    /// <summary>La fecha viable más cercana: se aleja de a un día, primero hacia adelante y luego hacia atrás.</summary>
    private static DiaDto? Sugerir(Ctx c, DateTime fecha, string complejidad)
    {
        var minimo = c.Hoy;
        var maximo = c.Hoy.AddDays(Catalogo.HorizonteDias - 1);
        for (var paso = 1; paso < Catalogo.HorizonteDias; paso++)
        {
            foreach (var candidata in new[] { fecha.AddDays(paso), fecha.AddDays(-paso) })
            {
                if (candidata < minimo || candidata > maximo) continue;
                if (Verificar(c, UsoDelDia(c, candidata), complejidad).Count == 0) return Dia(c, candidata);
            }
        }
        return null;
    }

    /// <summary>Para cada recurso, el día más cargado de cada una de 4 semanas (2 pasadas, la actual y la próxima), en %.</summary>
    private static Dictionary<string, List<int>> PicoSemanal(Ctx c)
    {
        var resultado = Catalogo.Recursos.ToDictionary(r => r, _ => new List<int>());
        foreach (var inicio in new[] { -14, -7, 0, 7 })
        {
            var picos = UsoVacio();
            for (var d = inicio; d < inicio + 7; d++)
            {
                var uso = UsoDelDia(c, c.Hoy.AddDays(d));
                foreach (var r in Catalogo.Recursos) picos[r] = Math.Max(picos[r], uso[r] / c.Capacidad[r]);
            }
            foreach (var r in Catalogo.Recursos) resultado[r].Add((int)Redondear(picos[r] * 100));
        }
        return resultado;
    }

    /// <summary>El recurso que más veces llega al límite en los próximos 14 días.</summary>
    private static CuelloDto CuelloDeBotella(Ctx c)
    {
        var dias = Enumerable.Range(0, Catalogo.HorizonteDias).Select(i => UsoDelDia(c, c.Hoy.AddDays(i))).ToList();
        var peor = Catalogo.Recursos
            .Select(r => new
            {
                Recurso = r,
                EnLimite = dias.Count(d => d[r] / c.Capacidad[r] >= 0.9m),
                Pico = dias.Max(d => d[r] / c.Capacidad[r]),
            })
            .OrderByDescending(x => x.EnLimite).ThenByDescending(x => x.Pico)
            .First();

        var nombre = Catalogo.NombresRecurso[peor.Recurso];
        return peor.EnLimite > 0
            ? new CuelloDto(nombre, $"llega al límite en {peor.EnLimite} de los próximos {Catalogo.HorizonteDias} días.")
            : new CuelloDto(nombre, $"es el más exigido, con un pico de {Redondear(peor.Pico * 100)}% de uso.");
    }

    // ==========================================================================
    // Costos y rentabilidad con alertas
    // ==========================================================================

    private static decimal CostoReceta(Receta receta, decimal escala, Dictionary<int, Insumo> insumos, Func<Insumo, decimal> precioDe)
    {
        var total = receta.ManoObra;
        foreach (var x in receta.Insumos)
            if (insumos.TryGetValue(x.InsumoId, out var insumo)) total += x.Cantidad * escala * precioDe(insumo);
        return total;
    }

    /// <summary>Costo con los precios de cuando SE COTIZÓ ese pedido, o con los de hoy.</summary>
    private static decimal Costo(Ctx c, Pedido p, bool cotizado)
    {
        var receta = c.RecetaPorId[p.RecetaId];
        var foto = p.PreciosCotizados.ToDictionary(x => x.InsumoId, x => x.Precio);
        return CostoReceta(receta, p.Escala, c.InsumoPorId, i =>
            cotizado ? foto.GetValueOrDefault(i.Id, i.PrecioCotizado) : i.PrecioActual ?? 0);
    }

    /// <summary>Lo ya entregado se produjo con los precios de entonces; lo pendiente, con los de hoy.</summary>
    private static decimal CostoReal(Ctx c, Pedido p) => Costo(c, p, cotizado: p.Estado == Catalogo.Entregado);

    private sealed record Evaluado(Pedido Pedido, decimal CostoCotizado, decimal CostoActual, decimal MargenCotizado,
        decimal MargenActual, decimal Caida, decimal PrecioSugerido, string Nivel, string Variante);

    private static Evaluado Evaluar(Ctx c, Pedido p)
    {
        var costoCotizado = Costo(c, p, cotizado: true);
        var costoActual = Costo(c, p, cotizado: false);
        var margenCotizado = (p.Precio - costoCotizado) / p.Precio * 100;
        var margenActual = (p.Precio - costoActual) / p.Precio * 100;
        var caida = margenCotizado - margenActual;
        var precioSugerido = Math.Ceiling(costoActual / (1 - margenCotizado / 100) / 100) * 100;

        var (nivel, variante) = caida >= 4 ? ("Alta", "rose") : caida >= 2 ? ("Media", "amber") : ("Leve", "neutral");
        return new Evaluado(p, costoCotizado, costoActual, margenCotizado, margenActual, caida, precioSugerido, nivel, variante);
    }

    private sealed record Rentabilidad(int Abiertos, List<Evaluado> Afectados, int InsumosConAlza,
        decimal CostoExtra, decimal MargenCotizado, decimal MargenHoy);

    private static Rentabilidad CalcularRentabilidad(Ctx c)
    {
        var abiertos = c.Pedidos.Where(EstaAbierto).ToList();
        var evaluados = abiertos.Select(p => Evaluar(c, p)).ToList();
        var afectados = evaluados.Where(e => e.CostoActual > e.CostoCotizado).OrderByDescending(e => e.Caida).ToList();

        var insumosConAlza = c.Insumos.Count(i => (i.PrecioActual ?? 0) > i.PrecioCotizado);
        var costoExtra = afectados.Sum(e => e.CostoActual - e.CostoCotizado);

        var ventas = abiertos.Sum(p => p.Precio);
        var costoCot = evaluados.Sum(e => e.CostoCotizado);
        var costoHoy = evaluados.Sum(e => e.CostoActual);

        return new Rentabilidad(abiertos.Count, afectados, insumosConAlza, costoExtra,
            ventas > 0 ? (ventas - costoCot) / ventas * 100 : 0,
            ventas > 0 ? (ventas - costoHoy) / ventas * 100 : 0);
    }

    public async Task<ResumenRentabilidadDto> RentabilidadAsync()
    {
        var c = await CargarAsync();
        var r = CalcularRentabilidad(c);
        var recetas = c.RecetaPorId;

        return new ResumenRentabilidadDto(r.MargenCotizado, r.MargenHoy, r.InsumosConAlza, r.CostoExtra, r.Abiertos,
            r.Afectados.Select(e => new PedidoEvaluadoDto(e.Pedido.Id, e.Pedido.Codigo, e.Pedido.Cliente, e.Pedido.Producto,
                e.Pedido.Precio, e.MargenCotizado, e.MargenActual, e.Caida, e.Nivel, e.Variante, e.PrecioSugerido)).ToList(),
            MargenPorMes(c));
    }

    /// <summary>Margen de los 5 meses anteriores (historial) + el mes actual (calculado con los pedidos).</summary>
    private static List<MargenMesDto> MargenPorMes(Ctx c)
    {
        var meses = c.Historial.TakeLast(5)
            .Select(h => new MargenMesDto(Meses[h.Mes - 1], h.Ingresos > 0 ? (int)Redondear((h.Ingresos - h.Costos) / h.Ingresos * 100) : 0))
            .ToList();

        var vigentes = c.Pedidos.Where(p => p.Estado != Catalogo.Cancelado).ToList();
        var ventas = vigentes.Sum(p => p.Precio);
        var costos = vigentes.Sum(p => CostoReal(c, p));
        meses.Add(new MargenMesDto(Meses[c.Hoy.Month - 1], ventas > 0 ? (int)Redondear((ventas - costos) / ventas * 100) : 0));
        return meses;
    }

    private static List<ProductoMargenDto> MargenPorProducto(Ctx c)
    {
        var recetas = c.RecetaPorId;
        return c.Pedidos.Where(p => p.Estado != Catalogo.Cancelado)
            .GroupBy(p => p.RecetaId)
            .Select(g =>
            {
                var ventas = g.Sum(p => p.Precio);
                var costo = g.Sum(p => CostoReal(c, p));
                return new ProductoMargenDto(recetas[g.Key].Nombre, (int)Redondear((ventas - costo) / ventas * 100));
            })
            .OrderByDescending(x => x.Margen)
            .ToList();
    }

    // ==========================================================================
    // Panel ejecutivo: junta los datos de los otros módulos
    // ==========================================================================

    public async Task<PanelDto> PanelAsync()
    {
        var c = await CargarAsync();
        var rent = CalcularRentabilidad(c);

        var vigentes = c.Pedidos.Where(p => p.Estado != Catalogo.Cancelado).ToList();
        var resumen = new ResumenPedidosDto(
            vigentes.Count,
            vigentes.Count(p => p.Estado == Catalogo.Entregado),
            vigentes.Count(EstaAbierto),
            c.Pedidos.Count - vigentes.Count,
            vigentes.Sum(p => p.Precio),
            vigentes.Count(p => p.Origen == "IA"));

        // Tendencia de ingresos (millones) y de cantidad de pedidos: historial + mes actual
        var mesActual = Meses[c.Hoy.Month - 1];
        var historial = c.Historial.TakeLast(5).ToList();
        var ingresos = historial.Select(h => new PuntoMensualDto(Meses[h.Mes - 1], Math.Round(h.Ingresos / 1_000_000m, 1, MidpointRounding.AwayFromZero))).ToList();
        ingresos.Add(new PuntoMensualDto(mesActual, Redondear(resumen.Ingresos / 100_000m) / 10m));

        var pedidosPorMes = historial.Select(h => new PuntoMensualDto(Meses[h.Mes - 1], h.NumPedidos)).ToList();
        pedidosPorMes.Add(new PuntoMensualDto(mesActual, resumen.Total));

        var conversion = new ConversionDto(c.Interpretaciones, resumen.PorIa,
            c.Interpretaciones > 0 ? Math.Min(100, (int)Redondear(resumen.PorIa * 100m / c.Interpretaciones)) : 0);

        var pico = PicoSemanal(c);
        var uso = Catalogo.Recursos.Select(r => new UsoRecursoDto(r, Catalogo.NombresRecurso[r], pico[r])).ToList();

        return new PanelDto(resumen, rent.MargenCotizado, rent.MargenHoy, ingresos, conversion, uso,
            CuelloDeBotella(c), MargenPorProducto(c), pedidosPorMes, Proyectar(pedidosPorMes.Select(p => (int)p.Valor).ToList()));
    }

    /// <summary>Pedidos del último mes + el promedio de crecimiento de los últimos 3 cambios.</summary>
    private static int Proyectar(List<int> valores)
    {
        var ultimos = valores.TakeLast(4).ToList();
        if (ultimos.Count < 2) return valores.LastOrDefault();
        decimal suma = 0;
        for (var i = 1; i < ultimos.Count; i++) suma += ultimos[i] - ultimos[i - 1];
        return (int)Redondear(valores[^1] + suma / (ultimos.Count - 1));
    }

    /// <summary>
    /// Le pasa el texto del cliente al servicio de NLP (Python + spaCy), guarda la solicitud con lo que entendió
    /// y devuelve esa interpretación junto con el id de la solicitud.
    /// </summary>
    public async Task<JsonNode> InterpretarAsync(string texto)
    {
        texto = (texto ?? "").Trim();
        if (texto == "") throw new NegocioException(400, "Escribe lo que necesitas.");
        if (texto.Length > 1000) throw new NegocioException(400, "El texto es demasiado largo (máximo 1000 letras).");

        var ahora = reloj.GetLocalNow().DateTime;
        var resultado = await nlp.InterpretarAsync(texto, ahora.Date);

        var solicitud = new SolicitudNlp { Texto = texto, ResultadoJson = resultado.ToJsonString(), CreadoEn = ahora };
        await repo.InsertarAsync([solicitud]);
        resultado["solicitudId"] = solicitud.Id;
        return resultado;
    }

    public async Task<SolicitudDto> RegistrarSolicitudAsync(NuevaSolicitudDto dto)
    {
        var texto = (dto.Texto ?? "").Trim();
        if (texto == "") throw new NegocioException(400, "El texto de la solicitud está vacío.");

        var solicitud = new SolicitudNlp
        {
            Texto = texto.Length > 1000 ? texto[..1000] : texto,
            ResultadoJson = dto.ResultadoJson,
            CreadoEn = reloj.GetLocalNow().DateTime,
        };
        await repo.InsertarAsync([solicitud]);
        return new SolicitudDto(solicitud.Id, solicitud.Texto, solicitud.CreadoEn);
    }

    // Ayudas

    private static decimal Redondear(decimal valor) => Math.Round(valor, 0, MidpointRounding.AwayFromZero);
    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
