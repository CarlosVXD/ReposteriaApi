namespace ReposteriaApi.Infra;

/// <summary>Un error esperado del negocio (dato inválido, sin capacidad, no existe...). Se responde con su código HTTP.</summary>
public class NegocioException(int estado, string mensaje, object? detalle = null) : Exception(mensaje)
{
    public int Estado { get; } = estado;
    public object? Detalle { get; } = detalle;
}

/// <summary>Convierte las NegocioException en respuestas JSON { error, detalle } con su código HTTP.</summary>
public class ManejadorErrores(RequestDelegate siguiente)
{
    public async Task Invoke(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (NegocioException ex)
        {
            contexto.Response.StatusCode = ex.Estado;
            await contexto.Response.WriteAsJsonAsync(new { error = ex.Message, detalle = ex.Detalle });
        }
    }
}
