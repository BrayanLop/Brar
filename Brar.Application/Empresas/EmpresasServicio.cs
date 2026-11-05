using Brar.Application.Auditoria;
using Brar.Application.Common;
using Brar.Application.Natural;

namespace Brar.Application.Empresas;

/// <summary>Casos de uso sobre tenants. Valida la entrada (§6 del plan) antes de llamar a Natural y audita cada acción.</summary>
public sealed class EmpresasServicio(INaturalPlataforma natural, AuditoriaServicio auditoria)
{
    private const int LongitudHashCliente = 44;

    /// <summary>Consulta: no se audita (la auditoría registra acciones que cambian datos o dan acceso).</summary>
    public Task<IReadOnlyList<EmpresaDto>> ListarAsync(Actor actor, CancellationToken ct) =>
        natural.ListarEmpresasAsync(actor, ct);

    public Task<EmpresaDto> CrearAsync(Actor actor, CrearEmpresaSolicitud? solicitud, CancellationToken ct)
    {
        if (solicitud is null) throw ErrorApi.Invalido("Debe enviar los datos de la empresa.");
        ValidarEmpresa(solicitud.Nombre, solicitud.Nit, solicitud.Direccion, solicitud.Telefono);

        var admin = solicitud.Admin ?? throw ErrorApi.Invalido("Debe enviar los datos del administrador de la empresa.");
        foreach (var (valor, campo) in new[]
                 {
                     (admin.Nombre, "nombre del administrador"), (admin.Apellido, "apellido del administrador"),
                     (admin.Cedula, "cédula del administrador"), (admin.Email, "correo del administrador"),
                     (admin.Celular, "celular del administrador")
                 })
        {
            if (string.IsNullOrWhiteSpace(valor)) throw ErrorApi.Invalido($"Falta el {campo}.");
        }

        var usuario = (admin.NombreUsuario ?? "").Trim();
        if (usuario.Length is < 3 or > 50 || usuario.Any(char.IsWhiteSpace))
            throw ErrorApi.Invalido("El nombre de usuario debe tener entre 3 y 50 caracteres, sin espacios.");
        if (string.IsNullOrWhiteSpace(admin.Contrasena) || admin.Contrasena.Length != LongitudHashCliente)
            throw ErrorApi.Invalido("La contraseña debe enviarse en formato correcto.");

        var detalle = $"Empresa '{solicitud.Nombre!.Trim()}'; admin '{usuario}'";
        return auditoria.EjecutarAsync(actor, "CrearEmpresa", null, null, detalle,
            () => natural.CrearEmpresaAsync(actor, solicitud with { Admin = admin with { NombreUsuario = usuario } }, ct), ct);
    }

    public Task ActualizarAsync(Actor actor, int empresaId, EmpresaSolicitud? solicitud, CancellationToken ct)
    {
        if (solicitud is null) throw ErrorApi.Invalido("Debe enviar los datos de la empresa.");
        ValidarEmpresa(solicitud.Nombre, solicitud.Nit, solicitud.Direccion, solicitud.Telefono);

        return auditoria.EjecutarAsync(actor, "ActualizarEmpresa", empresaId, null, $"Empresa '{solicitud.Nombre!.Trim()}'",
            () => natural.ActualizarEmpresaAsync(actor, empresaId, solicitud, ct), ct);
    }

    public Task CambiarEstadoAsync(Actor actor, int empresaId, bool activa, CancellationToken ct) =>
        auditoria.EjecutarAsync(actor, activa ? "ActivarEmpresa" : "DesactivarEmpresa", empresaId, null, null,
            () => natural.CambiarEstadoEmpresaAsync(actor, empresaId, activa, ct), ct);

    private static void ValidarEmpresa(string? nombre, string? nit, string? direccion, string? telefono)
    {
        var n = (nombre ?? "").Trim();
        if (n.Length is < 2 or > 100) throw ErrorApi.Invalido("El nombre de la empresa es obligatorio (máx. 100 caracteres).");
        if ((nit?.Trim().Length ?? 0) > 20) throw ErrorApi.Invalido("El NIT admite máximo 20 caracteres.");
        if ((telefono?.Trim().Length ?? 0) > 20) throw ErrorApi.Invalido("El teléfono admite máximo 20 caracteres.");
        if ((direccion?.Trim().Length ?? 0) > 200) throw ErrorApi.Invalido("La dirección admite máximo 200 caracteres.");
    }
}
