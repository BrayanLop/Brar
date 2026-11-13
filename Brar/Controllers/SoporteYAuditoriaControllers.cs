using Brar.Application.Auditoria;
using Brar.Application.Soporte;
using Microsoft.AspNetCore.Mvc;

namespace Brar.Controllers;

[Route("Empresas/{empresaId:int}/Impersonar")]
public class SoporteController(SoporteServicio soporte) : ControladorPlataformaBase
{
    /// <summary>Token temporal como administrador de la empresa. Exige motivo; queda auditado y registrado.</summary>
    [HttpPost]
    public async Task<IActionResult> Impersonar(int empresaId, [FromBody] ImpersonarSolicitudBrar? solicitud, CancellationToken ct) =>
        Ok(await soporte.ImpersonarAsync(Actor, empresaId, solicitud, ct));
}

[Route("Auditoria")]
public class AuditoriaController(AuditoriaConsultaServicio consulta) : ControladorPlataformaBase
{
    [HttpGet]
    public async Task<IActionResult> Consultar(
        [FromQuery] int? empresaId, [FromQuery] int? actorPersonaId,
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 25, CancellationToken ct = default) =>
        Ok(await consulta.ConsultarAsync(empresaId, actorPersonaId, desde, hasta, pagina, tamano, ct));
}
