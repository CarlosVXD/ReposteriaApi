using ReposteriaApi.Modelo;

namespace ReposteriaApi.Datos;

/// <summary>
/// Datos de ejemplo. Las fechas son relativas a "hoy" (dia = -4 es hace 4 días, 3 es dentro de 3 días),
/// así la agenda siempre tiene pedidos cerca de la fecha actual.
/// La usan los dos repositorios: cada uno solo aporta cómo insertar.
/// </summary>
public static class Semilla
{
    /// <summary>Precios de hoy con los que arrancan los insumos (para "restablecer").</summary>
    public static readonly Dictionary<string, decimal> PreciosActuales = new()
    {
        ["harina"] = 3456, ["azucar"] = 4200, ["mantequilla"] = 32000, ["huevos"] = 480,
        ["leche"] = 3800, ["chocolate"] = 40500, ["crema"] = 12000,
    };

    public static async Task AplicarAsync(Func<IEnumerable<object>, Task> insertar, DateTime ahora)
    {
        var hoy = ahora.Date;

        // 1) Insumos
        var insumos = new List<Insumo>
        {
            new() { Codigo = "harina", Nombre = "Harina", Unidad = "kg", PrecioCotizado = 3200 },
            new() { Codigo = "azucar", Nombre = "Azúcar", Unidad = "kg", PrecioCotizado = 4200 },
            new() { Codigo = "mantequilla", Nombre = "Mantequilla", Unidad = "kg", PrecioCotizado = 28000 },
            new() { Codigo = "huevos", Nombre = "Huevos", Unidad = "unidad", PrecioCotizado = 480 },
            new() { Codigo = "leche", Nombre = "Leche", Unidad = "litro", PrecioCotizado = 3800 },
            new() { Codigo = "chocolate", Nombre = "Chocolate", Unidad = "kg", PrecioCotizado = 36000 },
            new() { Codigo = "crema", Nombre = "Crema de leche", Unidad = "litro", PrecioCotizado = 12000 },
        };
        foreach (var i in insumos) i.PrecioActual = PreciosActuales[i.Codigo];
        await insertar(insumos);
        var idInsumo = insumos.ToDictionary(i => i.Codigo, i => i.Id);

        // 2) Capacidad diaria y historial de los 5 meses anteriores
        await insertar(Catalogo.CapacidadPorDefecto.Select(c => new Capacidad { Recurso = c.Key, HorasDia = c.Value }));

        decimal[] ingresos = [1_300_000, 1_500_000, 1_400_000, 1_800_000, 2_000_000];
        decimal[] costos = [800_000, 910_000, 860_000, 1_070_000, 1_180_000];
        int[] numPedidos = [11, 13, 12, 15, 16];
        var primerDiaMes = new DateTime(hoy.Year, hoy.Month, 1);
        var historial = new List<HistorialMensual>();
        for (var k = 0; k < 5; k++)
        {
            var mes = primerDiaMes.AddMonths(-(5 - k));
            historial.Add(new HistorialMensual
            {
                Anio = mes.Year, Mes = mes.Month,
                Ingresos = ingresos[k], Costos = costos[k], NumPedidos = numPedidos[k],
            });
        }
        await insertar(historial);

        // 3) Recetas (cada una trae sus insumos)
        Receta R(string codigo, string nombre, int tamano, decimal mano, params (string insumo, decimal cantidad)[] consumo) => new()
        {
            Codigo = codigo, Nombre = nombre, TamanoBase = tamano, ManoObra = mano,
            Insumos = consumo.Select(c => new RecetaInsumo { InsumoId = idInsumo[c.insumo], Cantidad = c.cantidad }).ToList(),
        };

        var recetas = new List<Receta>
        {
            R("tematica", "Tortas temáticas", 20, 30000,
                ("harina", 0.8m), ("azucar", 0.7m), ("mantequilla", 0.6m), ("huevos", 10), ("chocolate", 0.9m), ("crema", 0.8m)),
            R("clasica", "Tortas clásicas", 20, 18000,
                ("harina", 0.7m), ("azucar", 0.6m), ("mantequilla", 0.4m), ("huevos", 8), ("leche", 0.5m), ("chocolate", 0.4m)),
            R("cupcakes", "Cupcakes", 24, 14000,
                ("harina", 0.6m), ("azucar", 0.5m), ("mantequilla", 0.5m), ("huevos", 8), ("leche", 0.4m), ("chocolate", 0.3m)),
            R("tresleches", "Tortas tres leches", 20, 25000,
                ("harina", 0.5m), ("azucar", 0.6m), ("mantequilla", 0.2m), ("huevos", 12), ("leche", 2), ("crema", 1)),
            R("boda", "Tortas de boda", 60, 90000,
                ("harina", 2.5m), ("azucar", 2), ("mantequilla", 1.8m), ("huevos", 30), ("chocolate", 1.5m), ("crema", 2.5m), ("leche", 1)),
            R("postres", "Postres individuales", 12, 20000,
                ("azucar", 0.5m), ("huevos", 12), ("leche", 1.5m), ("crema", 1.2m)),
        };
        await insertar(recetas);
        var idReceta = recetas.ToDictionary(r => r.Codigo, r => r.Id);

        // 4) Pedidos de ejemplo: (cliente, producto, receta, complejidad, dia, estado, precio, origen)
        var datos = new (string cliente, string producto, string receta, string complejidad, int dia, string estado, decimal precio, string origen)[]
        {
            ("Julián Castro", "Postres individuales x12", "postres", "Media", -8, Catalogo.Entregado, 96000, "Manual"),
            ("Carolina Vega", "Cupcakes x24, chocolate", "cupcakes", "Media", -6, Catalogo.Entregado, 74000, "IA"),
            ("Esteban Cruz", "Torta temática superhéroes", "tematica", "Alta", -4, Catalogo.Entregado, 148000, "IA"),
            ("Marta Salcedo", "Torta 3 leches · 25 porciones", "tresleches", "Alta", -2, Catalogo.Entregado, 120000, "Manual"),
            ("Kevin Torres", "Postres individuales x12", "postres", "Media", 0, Catalogo.Listo, 94000, "IA"),
            ("Sofía Martínez", "Torta cumpleaños infantil", "tematica", "Alta", 1, Catalogo.EnProduccion, 152000, "Manual"),
            ("María Gómez", "Torta 3 leches · 20 porciones", "tresleches", "Alta", 2, Catalogo.EnProduccion, 110000, "IA"),
            ("Pedro López", "Cupcakes x24, vainilla", "cupcakes", "Media", 2, Catalogo.EnProduccion, 72000, "IA"),
            ("Camila Ortiz", "Torta temática mariposas", "tematica", "Alta", 3, Catalogo.Pendiente, 150000, "IA"),
            ("Laura Duarte", "Torta de boda · 3 pisos", "boda", "Alta", 5, Catalogo.Pendiente, 420000, "Manual"),
            ("Andrés Mejía", "Postres individuales x12", "postres", "Baja", 5, Catalogo.EnProduccion, 92000, "IA"),
            ("Ana Ramírez", "Torta temática unicornio", "tematica", "Alta", 7, Catalogo.Pendiente, 150000, "IA"),
            ("Daniela Pérez", "Cupcakes x24, chocolate", "cupcakes", "Media", 7, Catalogo.EnProduccion, 74000, "Manual"),
            ("Valentina Ríos", "Torta 3 leches · 30 porciones", "tresleches", "Alta", 9, Catalogo.EnProduccion, 128000, "IA"),
            ("Lucía Fernández", "Torta temática princesa", "tematica", "Alta", 10, Catalogo.Pendiente, 155000, "Manual"),
            ("Hugo Barrios", "Postres individuales x12", "postres", "Baja", 10, Catalogo.Pendiente, 90000, "IA"),
            ("Mateo Herrera", "Torta de bautizo", "tematica", "Alta", 12, Catalogo.Pendiente, 148000, "Manual"),
            ("Isabela Cano", "Postres individuales x12", "postres", "Media", 12, Catalogo.Pendiente, 96000, "IA"),
        };

        // 5) Solicitudes de la Captura: una por cada pedido de IA, más 3 que no se convirtieron en pedido
        var solicitudes = new List<SolicitudNlp>();
        foreach (var d in datos.Where(x => x.origen == "IA"))
        {
            solicitudes.Add(new SolicitudNlp { Texto = $"Pedido de {d.cliente}: {d.producto}", CreadoEn = hoy.AddDays(d.dia - 3) });
        }
        solicitudes.Add(new SolicitudNlp { Texto = "Cotización de una torta para 15 personas", CreadoEn = hoy.AddDays(-5) });
        solicitudes.Add(new SolicitudNlp { Texto = "Cupcakes para una reunión de oficina", CreadoEn = hoy.AddDays(-3) });
        solicitudes.Add(new SolicitudNlp { Texto = "Postre para un aniversario", CreadoEn = hoy.AddDays(-1) });
        await insertar(solicitudes);

        // 6) Pedidos. Todos se cotizaron con la lista de precios base de los insumos.
        var siguienteSolicitud = 0;
        var pedidos = new List<Pedido>();
        foreach (var d in datos)
        {
            pedidos.Add(new Pedido
            {
                Codigo = $"P-{pedidos.Count + 1:000}",
                Cliente = d.cliente, Producto = d.producto,
                RecetaId = idReceta[d.receta], Complejidad = d.complejidad,
                Fecha = hoy.AddDays(d.dia), Estado = d.estado, Precio = d.precio, Origen = d.origen,
                Escala = 1, CreadoEn = hoy.AddDays(d.dia - 3),
                SolicitudId = d.origen == "IA" ? solicitudes[siguienteSolicitud++].Id : null,
                PreciosCotizados = insumos.Select(i => new PedidoPrecio { InsumoId = i.Id, Precio = i.PrecioCotizado }).ToList(),
            });
        }
        await insertar(pedidos);
    }
}
