using Brar.Application.Auditoria;
using Brar.Application.Common;
using Brar.Application.Natural;

namespace Brar.Application.Accesos;

/// <summary>Accesos (usuarios) de una empresa. Valida antes de llamar a Natural (§6 del plan); Natural repite las reglas y protege al último admin.</summary>
public sealed class AccesosServicio(INaturalPlataforma natural, AuditoriaServicio auditoria)
{
    private const int LongitudHashCliente = 44;
    private static readonly string[] RolesAsignables = ["01", "02", "03"];

    public Task<IReadOnlyList<AccesoDto>> ListarAsync(Actor actor, int empresaId, CancellationToken ct) =>
        natural.ListarAccesosAsync(actor, empresaId, ct);

    public Task<AccesoDto> CrearAsync(Actor actor, int empresaId, CrearAccesoSolicitud? solicitud, CancellationToken ct)
    {
        if (solicitud is null) throw ErrorApi.Invalido("Debe enviar los datos del acceso.");
        if (solicitud.PersonaId <= 0) throw ErrorApi.Invalido("Debe indicar la persona.");

        var usuario = (solicitud.NombreUsuario ?? "").Trim();
        if (usuario.Length is < 3 or > 50 || usuario.Any(char.IsWhiteSpace))
            throw ErrorApi.Invalido("El nombre de usuario debe tener entre 3 y 50 caracteres, sin espacios.");
        ValidarContrasena(solicitud.Contrasena);
        ValidarRol(solicitud.Rol);

        var detalle = $"Persona {solicitud.PersonaId}; usuario '{usuario}'; rol {solicitud.Rol}";
        return auditoria.EjecutarAsync(actor, "CrearAcceso", empresaId, null, detalle,
            () => natural.CrearAccesoAsync(actor, empresaId, solicitud with { NombreUsuario = usuario }, ct), ct);
    }

    public Task CambiarEstadoAsync(Actor actor, int empresaId, int usuarioId, bool activo, CancellationToken ct) =>
        auditoria.EjecutarAsync(actor, activo ? "ActivarAcceso" : "DesactivarAcceso", empresaId, usuarioId, null,
            () => natural.CambiarEstadoAccesoAsync(actor, empresaId, usuarioId, activo, ct), ct);

    public Task CambiarRolAsync(Actor actor, int empresaId, int usuarioId, CambiarRolSolicitud? solicitud, CancellationToken ct)
    {
        ValidarRol(solicitud?.Rol);
        return auditoria.EjecutarAsync(actor, "CambiarRolAcceso", empresaId, usuarioId, $"Nuevo rol {solicitud!.Rol}",
            () => natural.CambiarRolAccesoAsync(actor, empresaId, usuarioId, solicitud.Rol, ct), ct);
    }

    public Task RestablecerContrasenaAsync(Actor actor, int empresaId, int usuarioId, RestablecerContrasenaSolicitud? solicitud, CancellationToken ct)
    {
        ValidarContrasena(solicitud?.NuevaContrasena);
        // El detalle no lleva datos de la contraseña.
        return auditoria.EjecutarAsync(actor, "RestablecerContrasena", empresaId, usuarioId, null,
            () => natural.RestablecerContrasenaAsync(actor, empresaId, usuarioId, solicitud!.NuevaContrasena, ct), ct);
    }

    public Task EliminarAsync(Actor actor, int empresaId, int usuarioId, CancellationToken ct) =>
        auditoria.EjecutarAsync(actor, "EliminarAcceso", empresaId, usuarioId, null,
            () => natural.EliminarAccesoAsync(actor, empresaId, usuarioId, ct), ct);

    private static void ValidarContrasena(string? contrasena)
    {
        if (string.IsNullOrWhiteSpace(contrasena) || contrasena.Length != LongitudHashCliente)
            throw ErrorApi.Invalido("La contraseña debe enviarse en formato correcto.");
    }

    private static void ValidarRol(string? rol)
    {
        if (rol is null || !RolesAsignables.Contains(rol))
            throw ErrorApi.Invalido("El rol no es válido (01, 02 o 03).");
    }
}
