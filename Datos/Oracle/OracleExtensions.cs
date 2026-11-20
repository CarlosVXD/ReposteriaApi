using Microsoft.EntityFrameworkCore;
using Oracle.EntityFrameworkCore;

namespace ReposteriaApi.Datos.Oracle;

public static class OracleExtensions
{
    public static IServiceCollection AddRepositorioOracle(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        var conexion = configuracion.GetConnectionString("Oracle")
            ?? throw new InvalidOperationException(
                "Falta ConnectionStrings:Oracle en appsettings.json");

        servicios.AddDbContext<ReposteriaDbContext>(o =>
            o.UseOracle(conexion, oracle =>
                oracle.UseOracleSQLCompatibility(
                    OracleSQLCompatibility.DatabaseVersion19)));

        servicios.AddScoped<IRepositorio, RepositorioOracle>();

        return servicios;
    }
}