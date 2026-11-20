using Microsoft.EntityFrameworkCore;
using ReposteriaApi.Modelo;

namespace ReposteriaApi.Datos.Oracle;

public class RepositorioOracle(ReposteriaDbContext db) : IRepositorio
{
    public Task<List<Pedido>> PedidosAsync() =>
        db.Pedidos.Include(p => p.PreciosCotizados).OrderBy(p => p.Id).ToListAsync();

    public Task<Pedido?> PedidoAsync(int id) =>
        db.Pedidos.Include(p => p.PreciosCotizados).FirstOrDefaultAsync(p => p.Id == id);

    public Task<List<Insumo>> InsumosAsync() => db.Insumos.OrderBy(i => i.Id).ToListAsync();

    public Task<List<Receta>> RecetasAsync() => db.Recetas.Include(r => r.Insumos).OrderBy(r => r.Id).ToListAsync();

    public Task<List<Capacidad>> CapacidadesAsync() => db.Capacidades.ToListAsync();

    public Task<List<HistorialMensual>> HistorialAsync() => db.Historial.OrderBy(h => h.Anio).ThenBy(h => h.Mes).ToListAsync();

    public Task<int> ContarSolicitudesAsync() => db.Solicitudes.CountAsync();

    public Task<bool> ExisteSolicitudAsync(int id) => db.Solicitudes.AnyAsync(s => s.Id == id);

    public async Task<bool> EstaVacioAsync() => !await db.Insumos.AnyAsync();

    public async Task InsertarAsync(IEnumerable<object> entidades)
    {
        db.AddRange(entidades);
        await db.SaveChangesAsync();
    }

    public Task GuardarAsync() => db.SaveChangesAsync();
}
