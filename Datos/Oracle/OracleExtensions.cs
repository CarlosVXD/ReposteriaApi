using Microsoft.EntityFrameworkCore;

namespace ReposteriaApi.Datos.Oracle;

public static class OracleExtensions
{
    /// <summary>Registra la conexión a Oracle y el repositorio que la usa.</summary>
    public static IServiceCollection AddRepositorioOracle(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var conexion = configuracion.GetConnectionString("Oracle")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Oracle en appsettings.json");

        servicios.AddDbContext<ReposteriaDbContext>(o => o.UseOracle(conexion));
        servicios.AddScoped<IRepositorio, RepositorioOracle>();
        return servicios;
    }
}
