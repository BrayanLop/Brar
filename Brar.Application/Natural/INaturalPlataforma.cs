using Brar.Application.Common;

namespace Brar.Application.Natural;

/// <summary>
/// Puerto hacia la API interna <c>api/Plataforma/*</c> de Natural (autenticada con clave de servicio).
/// Los errores de Natural se lanzan como <see cref="ErrorApi"/> con el status que corresponde a Brar.
/// </summary>
public interface INaturalPlataforma
{
    Task<IReadOnlyList<EmpresaDto>> ListarEmpresasAsync(Actor actor, CancellationToken ct);
    Task<EmpresaDto> CrearEmpresaAsync(Actor actor, CrearEmpresaSolicitud solicitud, CancellationToken ct);
    Task ActualizarEmpresaAsync(Actor actor, int empresaId, EmpresaSolicitud solicitud, CancellationToken ct);
    Task CambiarEstadoEmpresaAsync(Actor actor, int empresaId, bool activa, CancellationToken ct);

    Task<IReadOnlyList<AccesoDto>> ListarAccesosAsync(Actor actor, int empresaId, CancellationToken ct);
    Task<AccesoDto> CrearAccesoAsync(Actor actor, int empresaId, CrearAccesoSolicitud solicitud, CancellationToken ct);
    Task CambiarEstadoAccesoAsync(Actor actor, int empresaId, int usuarioId, bool activo, CancellationToken ct);
    Task CambiarRolAccesoAsync(Actor actor, int empresaId, int usuarioId, string? rol, CancellationToken ct);
    Task RestablecerContrasenaAsync(Actor actor, int empresaId, int usuarioId, string? nuevaContrasena, CancellationToken ct);
    Task EliminarAccesoAsync(Actor actor, int empresaId, int usuarioId, CancellationToken ct);

    Task<ImpersonacionDto> ImpersonarAsync(Actor actor, int empresaId, int minutos, CancellationToken ct);
}
