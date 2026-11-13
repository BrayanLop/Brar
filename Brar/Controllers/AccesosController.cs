using Brar.Application.Accesos;
using Brar.Application.Natural;
using Microsoft.AspNetCore.Mvc;

namespace Brar.Controllers;

[Route("Empresas/{empresaId:int}/Accesos")]
public class AccesosController(AccesosServicio accesos) : ControladorPlataformaBase
{
    [HttpGet]
    public async Task<IActionResult> Listar(int empresaId, CancellationToken ct) =>
        Ok(await accesos.ListarAsync(Actor, empresaId, ct));

    [HttpPost]
    public async Task<IActionResult> Crear(int empresaId, [FromBody] CrearAccesoSolicitud? solicitud, CancellationToken ct)
    {
        var acceso = await accesos.CrearAsync(Actor, empresaId, solicitud, ct);
        return StatusCode(StatusCodes.Status201Created, acceso);
    }

    [HttpPatch("{usuarioId:int}/Estado")]
    public async Task<IActionResult> CambiarEstado(int empresaId, int usuarioId, [FromQuery] bool activo, CancellationToken ct)
    {
        await accesos.CambiarEstadoAsync(Actor, empresaId, usuarioId, activo, ct);
        return Ok(new { activo });
    }

    [HttpPatch("{usuarioId:int}/Rol")]
    public async Task<IActionResult> CambiarRol(int empresaId, int usuarioId, [FromBody] CambiarRolSolicitud? solicitud, CancellationToken ct)
    {
        await accesos.CambiarRolAsync(Actor, empresaId, usuarioId, solicitud, ct);
        return Ok(new { rol = solicitud!.Rol });
    }

    [HttpPut("{usuarioId:int}/Contrasena")]
    public async Task<IActionResult> RestablecerContrasena(
        int empresaId, int usuarioId, [FromBody] RestablecerContrasenaSolicitud? solicitud, CancellationToken ct)
    {
        await accesos.RestablecerContrasenaAsync(Actor, empresaId, usuarioId, solicitud, ct);
        return Ok(new { mensaje = "Contraseña restablecida correctamente." });
    }

    [HttpDelete("{usuarioId:int}")]
    public async Task<IActionResult> Eliminar(int empresaId, int usuarioId, CancellationToken ct)
    {
        await accesos.EliminarAsync(Actor, empresaId, usuarioId, ct);
        return NoContent();
    }
}
