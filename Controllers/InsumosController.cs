using Microsoft.AspNetCore.Mvc;
using ReposteriaApi.Modelo;
using ReposteriaApi.Servicios;

namespace ReposteriaApi.Controllers;

[ApiController, Route("api/insumos")]
public class InsumosController(NegocioService negocio) : ControllerBase
{
    [HttpGet]
    public Task<List<InsumoDto>> Listar() => negocio.ListarInsumosAsync();

    /// <summary>Cambia el precio de hoy de un insumo. Con esto se recalculan las alertas de Rentabilidad.</summary>
    [HttpPut("{id:int}/precio")]
    public Task<InsumoDto> CambiarPrecio(int id, PrecioInsumoDto dto) => negocio.CambiarPrecioInsumoAsync(id, dto.PrecioActual);

    /// <summary>Vuelve los precios de hoy a los de ejemplo (solo para repetir la demostración).</summary>
    [HttpPost("restablecer")]
    public Task<List<InsumoDto>> Restablecer() => negocio.RestablecerInsumosAsync();
}

[ApiController, Route("api/recetas")]
public class RecetasController(NegocioService negocio) : ControllerBase
{
    [HttpGet]
    public Task<List<RecetaDto>> Listar() => negocio.ListarRecetasAsync();
}
