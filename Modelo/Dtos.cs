namespace ReposteriaApi.Modelo;

// Lo que viaja por HTTP. Los nombres coinciden con los del frontend en Angular (camelCase en el JSON).

// ---------- Pedidos ----------
public record PedidoDto(
    int Id, string Codigo, string Cliente, string Producto, string Receta, string Complejidad,
    DateTime Fecha, string Estado, decimal Precio, string Origen, decimal Escala);

public record NuevoPedidoDto(
    string Cliente, string? Producto, string Receta, decimal Escala, string Complejidad,
    DateTime Fecha, decimal? Precio, int? SolicitudId);

public record CambioEstadoDto(string Estado);

// ---------- Insumos y recetas ----------
public record InsumoDto(int Id, string Codigo, string Nombre, string Unidad, decimal PrecioCotizado, decimal? PrecioActual);
public record PrecioInsumoDto(decimal? PrecioActual);
public record RecetaInsumoDto(string Insumo, decimal Cantidad);
public record RecetaDto(int Id, string Codigo, string Nombre, int TamanoBase, decimal ManoObra, List<RecetaInsumoDto> Insumos);

// ---------- Cotización ----------
public record CotizarDto(string? Sabor, string? Tamano, string? Diseno, string? Tipo = null);
public record CotizacionDto(string Receta, decimal Escala, decimal Costo, decimal Precio, decimal MargenObjetivo);

// ---------- Agenda ----------
public record UsoDto(decimal Horno, decimal Decoracion, decimal Personal);
public record DiaDto(DateTime Fecha, UsoDto Usado, int Ocupacion, string Estado);
public record VerificarDto(DateTime Fecha, string Complejidad);
public record FaltanteDto(string Recurso, string Nombre, decimal Necesita, decimal Quedan);
public record VerificacionDto(DateTime Fecha, bool Cabe, List<FaltanteDto> Faltantes, DiaDto? Sugerencia);
public record CapacidadDto(decimal Horno, decimal Decoracion, decimal Personal);
public record ReglasDto(CapacidadDto Capacidad, Dictionary<string, UsoDto> Consumo);

// ---------- Rentabilidad ----------
public record PedidoEvaluadoDto(
    int Id, string Codigo, string Cliente, string Producto, decimal Precio,
    decimal MargenCotizado, decimal MargenActual, decimal Caida, string Nivel, string Variante, decimal PrecioSugerido);

public record MargenMesDto(string Mes, int Margen);

public record ResumenRentabilidadDto(
    decimal MargenCotizado, decimal MargenHoy, int InsumosConAlza, decimal CostoExtra,
    int Abiertos, List<PedidoEvaluadoDto> Afectados, List<MargenMesDto> Meses);

// ---------- Panel ejecutivo ----------
public record ResumenPedidosDto(int Total, int Entregados, int EnCurso, int Cancelados, decimal Ingresos, int PorIa);
public record PuntoMensualDto(string Mes, decimal Valor);
public record ConversionDto(int Interpretaciones, int Confirmadas, int Porcentaje);
public record UsoRecursoDto(string Recurso, string Nombre, List<int> Semanas);
public record CuelloDto(string Nombre, string Detalle);
public record ProductoMargenDto(string Nombre, int Margen);

public record PanelDto(
    ResumenPedidosDto Pedidos, decimal MargenCotizado, decimal MargenHoy,
    List<PuntoMensualDto> Ingresos, ConversionDto Conversion,
    List<UsoRecursoDto> Uso, CuelloDto Cuello, List<ProductoMargenDto> Productos,
    List<PuntoMensualDto> PedidosPorMes, int Proyeccion);

// ---------- Captura (NLP) ----------
public record NuevaSolicitudDto(string Texto, string? ResultadoJson);
public record InterpretarDto(string Texto);
public record SolicitudDto(int Id, string Texto, DateTime CreadoEn);
