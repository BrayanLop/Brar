using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brar.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    /// <summary>Para el healthcheck del contenedor / Railway. No toca la BD ni Natural.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new { estado = "ok", servicio = "brar" });
}
