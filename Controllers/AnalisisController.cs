using Microsoft.AspNetCore.Mvc;
using ReposteriaApi.Modelo;
using ReposteriaApi.Servicios;

namespace ReposteriaApi.Controllers;

[ApiController, Route("api/rentabilidad")]
public class RentabilidadController(NegocioService negocio) : ControllerBase
{
    /// <summary>Pedidos cotizados que se ven afectados por alzas de insumos, y el margen por mes.</summary>
    [HttpGet]
    public Task<ResumenRentabilidadDto> Resumen() => negocio.RentabilidadAsync();
}

[ApiController, Route("api/panel")]
public class PanelController(NegocioService negocio) : ControllerBase
{
    /// <summary>Todo lo que muestra el Panel Ejecutivo, en una sola llamada.</summary>
    [HttpGet]
    public Task<PanelDto> Panel() => negocio.PanelAsync();
}
