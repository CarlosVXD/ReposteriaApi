using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ReposteriaApi.Infra;

/// <summary>Habla con el servicio de NLP (Python + spaCy) que entiende los pedidos escritos en lenguaje natural.</summary>
public class NlpClient(HttpClient http)
{
    public async Task<JsonNode> InterpretarAsync(string texto, DateTime hoy)
    {
        try
        {
            using var respuesta = await http.PostAsJsonAsync("interpretar", new { texto, hoy = hoy.ToString("yyyy-MM-dd") });
            if (!respuesta.IsSuccessStatusCode)
                throw new NegocioException(502, $"El servicio de interpretación respondió {(int)respuesta.StatusCode}.");

            return await respuesta.Content.ReadFromJsonAsync<JsonNode>()
                ?? throw new NegocioException(502, "El servicio de interpretación devolvió una respuesta vacía.");
        }
        catch (HttpRequestException)
        {
            throw new NegocioException(503, "El servicio de interpretación no está disponible.");
        }
        catch (TaskCanceledException)
        {
            throw new NegocioException(503, "El servicio de interpretación tardó demasiado en responder.");
        }
    }
}