using Microsoft.AspNetCore.Mvc;
using ReposteriaApi.Modelo;
using ReposteriaApi.Servicios;

namespace ReposteriaApi.Controllers;

[ApiController, Route("api/captura")]
public class CapturaController(NegocioService negocio) : ControllerBase
{
    /// <summary>Con lo que la Captura entendió (sabor, tamaño, diseño): receta, escala, costo y precio sugerido.</summary>
    [HttpPost("cotizar")]
    public Task<CotizacionDto> Cotizar(CotizarDto dto) => negocio.CotizarAsync(dto);

    /// <summary>Interpreta con NLP lo que escribió el cliente y registra la solicitud.</summary>
    [HttpPost("interpretar")]
    public async Task<IActionResult> Interpretar(InterpretarDto dto)
    {
        var resultado = await negocio.InterpretarAsync(dto.Texto);
        return Content(resultado.ToJsonString(), "application/json");
    }

    /// <summary>Registra que se interpretó un texto. Alimenta la conversión de la IA en el Panel.</summary>
    [HttpPost("solicitudes")]
    public async Task<SolicitudDto> RegistrarSolicitud(NuevaSolicitudDto dto) => await negocio.RegistrarSolicitudAsync(dto);
}

[ApiController, Route("api/salud")]
public class SaludController : ControllerBase
{
    [HttpGet]
    public object Estado() => new { estado = "ok", hora = DateTime.Now };
}
