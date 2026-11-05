using Brar.Application.Common;
using Brar.Autenticacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brar.Controllers;

/// <summary>Base de los controllers de plataforma: solo rol 00, y expone el superadmin que llama.</summary>
[ApiController]
[Authorize(Policy = Politicas.Plataforma)]
public abstract class ControladorPlataformaBase : ControllerBase
{
    protected Actor Actor => new(
        int.Parse(User.FindFirst(BrarClaims.PersonaId)!.Value),
        User.FindFirst(BrarClaims.Nombre)?.Value ?? "desconocido",
        HttpContext.Connection.RemoteIpAddress?.ToString());
}
