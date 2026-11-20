using ReposteriaApi.Modelo;

namespace ReposteriaApi.Datos;

/// <summary>
/// Todo lo que el servicio de negocio necesita de la base de datos.
/// Hay dos implementaciones: Oracle (real) y Memoria (para pruebas sin base de datos).
/// </summary>
public interface IRepositorio
{
    Task<List<Pedido>> PedidosAsync();                 // con PreciosCotizados cargados
    Task<Pedido?> PedidoAsync(int id);
    Task<List<Insumo>> InsumosAsync();
    Task<List<Receta>> RecetasAsync();                 // con Insumos cargados
    Task<List<Capacidad>> CapacidadesAsync();
    Task<List<HistorialMensual>> HistorialAsync();
    Task<int> ContarSolicitudesAsync();
    Task<bool> ExisteSolicitudAsync(int id);
    Task<bool> EstaVacioAsync();

    /// <summary>Inserta entidades nuevas (con lo que cuelga de ellas) y les asigna su Id.</summary>
    Task InsertarAsync(IEnumerable<object> entidades);

    /// <summary>Guarda los cambios hechos a entidades que ya estaban cargadas.</summary>
    Task GuardarAsync();
}
