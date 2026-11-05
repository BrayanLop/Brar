using Brar.Application.Empresas;
using Brar.Application.Natural;
using Microsoft.AspNetCore.Mvc;

namespace Brar.Controllers;

[Route("Empresas")]
public class EmpresasController(EmpresasServicio empresas) : ControladorPlataformaBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) =>
        Ok(await empresas.ListarAsync(Actor, ct));

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearEmpresaSolicitud? solicitud, CancellationToken ct)
    {
        var empresa = await empresas.CrearAsync(Actor, solicitud, ct);
        return StatusCode(StatusCodes.Status201Created, empresa);
    }

    [HttpPut("{empresaId:int}")]
    public async Task<IActionResult> Actualizar(int empresaId, [FromBody] EmpresaSolicitud? solicitud, CancellationToken ct)
    {
        await empresas.ActualizarAsync(Actor, empresaId, solicitud, ct);
        return NoContent();
    }

    [HttpPatch("{empresaId:int}/Estado")]
    public async Task<IActionResult> CambiarEstado(int empresaId, [FromQuery] bool activa, CancellationToken ct)
    {
        await empresas.CambiarEstadoAsync(Actor, empresaId, activa, ct);
        return Ok(new { activa });
    }
}
