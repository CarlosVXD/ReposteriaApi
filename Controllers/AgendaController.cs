using Microsoft.AspNetCore.Mvc;
using ReposteriaApi.Modelo;
using ReposteriaApi.Servicios;

namespace ReposteriaApi.Controllers;

[ApiController, Route("api/agenda")]
public class AgendaController(NegocioService negocio) : ControllerBase
{
    /// <summary>Los próximos días con las horas ya ocupadas de horno, decoración y personal.</summary>
    [HttpGet("dias")]
    public Task<List<DiaDto>> Dias([FromQuery] int cantidad = Catalogo.HorizonteDias) => negocio.DiasAsync(cantidad);

    /// <summary>¿Cabe un pedido de esa complejidad ese día? Si no, dice qué recurso falla y sugiere otra fecha.</summary>
    [HttpPost("verificar")]
    public Task<VerificacionDto> Verificar(VerificarDto dto) => negocio.VerificarAsync(dto);

    /// <summary>Capacidad por día y horas que gasta cada complejidad, para dibujar la agenda.</summary>
    [HttpGet("reglas")]
    public Task<ReglasDto> Reglas() => negocio.ReglasAsync();

    [HttpGet("capacidad")]
    public Task<CapacidadDto> Capacidad() => negocio.ObtenerCapacidadAsync();

    [HttpPut("capacidad")]
    public Task<CapacidadDto> ActualizarCapacidad(CapacidadDto dto) => negocio.ActualizarCapacidadAsync(dto);
}
