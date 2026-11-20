using Microsoft.EntityFrameworkCore;
using ReposteriaApi.Modelo;

namespace ReposteriaApi.Datos.Oracle;

/// <summary>
/// El mapeo de tablas y columnas está en los atributos de Modelo/Entidades.cs,
/// por eso aquí solo se listan las tablas.
/// </summary>
public class ReposteriaDbContext(DbContextOptions<ReposteriaDbContext> opciones) : DbContext(opciones)
{
    public DbSet<Insumo> Insumos => Set<Insumo>();
    public DbSet<Receta> Recetas => Set<Receta>();
    public DbSet<RecetaInsumo> RecetaInsumos => Set<RecetaInsumo>();
    public DbSet<Capacidad> Capacidades => Set<Capacidad>();
    public DbSet<HistorialMensual> Historial => Set<HistorialMensual>();
    public DbSet<SolicitudNlp> Solicitudes => Set<SolicitudNlp>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<PedidoPrecio> PedidoPrecios => Set<PedidoPrecio>();
}
