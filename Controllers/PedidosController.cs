using Microsoft.AspNetCore.Mvc;
using ReposteriaApi.Modelo;
using ReposteriaApi.Servicios;

namespace ReposteriaApi.Controllers;

[ApiController, Route("api/pedidos")]
public class PedidosController(NegocioService negocio) : ControllerBase
{
    /// <summary>GET /api/pedidos?estado=Entregado&amp;q=maria</summary>
    [HttpGet]
    public Task<List<PedidoDto>> Listar([FromQuery] string? estado, [FromQuery] string? q) =>
        negocio.ListarPedidosAsync(estado, q);

    [HttpGet("{id:int}")]
    public Task<PedidoDto> Obtener(int id) => negocio.ObtenerPedidoAsync(id);

    /// <summary>Crea un pedido. Responde 409 si la fecha no tiene capacidad (e incluye la fecha sugerida).</summary>
    [HttpPost]
    public async Task<IActionResult> Crear(NuevoPedidoDto dto)
    {
        var pedido = await negocio.CrearPedidoAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id = pedido.Id }, pedido);
    }

    [HttpPatch("{id:int}/estado")]
    public Task<PedidoDto> CambiarEstado(int id, CambioEstadoDto dto) => negocio.CambiarEstadoAsync(id, dto.Estado);
}
