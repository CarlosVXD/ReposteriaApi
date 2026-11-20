using ReposteriaApi.Modelo;

namespace ReposteriaApi.Datos;

/// <summary>
/// Guarda todo en listas en memoria. No necesita Oracle: sirve para pruebas y para trabajar el frontend
/// mientras la base de datos no está lista. Los datos se pierden al apagar el servidor.
/// </summary>
public class RepositorioMemoria : IRepositorio
{
    private readonly object _candado = new();
    private readonly Dictionary<Type, int> _contadores = new();

    private readonly List<Insumo> _insumos = [];
    private readonly List<Receta> _recetas = [];
    private readonly List<Capacidad> _capacidades = [];
    private readonly List<HistorialMensual> _historial = [];
    private readonly List<SolicitudNlp> _solicitudes = [];
    private readonly List<Pedido> _pedidos = [];

    public RepositorioMemoria()
    {
        Semilla.AplicarAsync(InsertarAsync, DateTime.Now).GetAwaiter().GetResult();
    }

    public Task<List<Pedido>> PedidosAsync() => Copia(_pedidos);
    public Task<Pedido?> PedidoAsync(int id) { lock (_candado) return Task.FromResult(_pedidos.FirstOrDefault(p => p.Id == id)); }
    public Task<List<Insumo>> InsumosAsync() => Copia(_insumos);
    public Task<List<Receta>> RecetasAsync() => Copia(_recetas);
    public Task<List<Capacidad>> CapacidadesAsync() => Copia(_capacidades);
    public Task<List<HistorialMensual>> HistorialAsync() => Copia(_historial);
    public Task<int> ContarSolicitudesAsync() { lock (_candado) return Task.FromResult(_solicitudes.Count); }
    public Task<bool> ExisteSolicitudAsync(int id) { lock (_candado) return Task.FromResult(_solicitudes.Any(s => s.Id == id)); }
    public Task<bool> EstaVacioAsync() { lock (_candado) return Task.FromResult(_insumos.Count == 0); }

    // Las entidades ya son los mismos objetos que guarda la lista: no hay nada que "guardar".
    public Task GuardarAsync() => Task.CompletedTask;

    public Task InsertarAsync(IEnumerable<object> entidades)
    {
        lock (_candado)
        {
            foreach (var e in entidades) Agregar(e);
        }
        return Task.CompletedTask;
    }

    private void Agregar(object entidad)
    {
        switch (entidad)
        {
            case Insumo i:
                i.Id = Siguiente<Insumo>(); _insumos.Add(i); break;
            case Receta r:
                r.Id = Siguiente<Receta>();
                foreach (var ri in r.Insumos) { ri.Id = Siguiente<RecetaInsumo>(); ri.RecetaId = r.Id; }
                _recetas.Add(r); break;
            case Capacidad c:
                c.Id = Siguiente<Capacidad>(); _capacidades.Add(c); break;
            case HistorialMensual h:
                h.Id = Siguiente<HistorialMensual>(); _historial.Add(h); break;
            case SolicitudNlp s:
                s.Id = Siguiente<SolicitudNlp>(); _solicitudes.Add(s); break;
            case Pedido p:
                p.Id = Siguiente<Pedido>();
                foreach (var pp in p.PreciosCotizados) { pp.Id = Siguiente<PedidoPrecio>(); pp.PedidoId = p.Id; }
                _pedidos.Add(p); break;
            default:
                throw new ArgumentException($"No sé guardar un {entidad.GetType().Name}");
        }
    }

    private int Siguiente<T>()
    {
        _contadores[typeof(T)] = _contadores.GetValueOrDefault(typeof(T)) + 1;
        return _contadores[typeof(T)];
    }

    private Task<List<T>> Copia<T>(List<T> origen)
    {
        lock (_candado) return Task.FromResult(new List<T>(origen));
    }
}
