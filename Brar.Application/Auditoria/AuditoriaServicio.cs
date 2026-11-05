using Brar.Application.Common;

namespace Brar.Application.Auditoria;

public interface IAuditoriaRepositorio
{
    /// <summary>Guarda la auditoría (queda con su Id generado).</summary>
    Task AgregarAsync(Domain.Auditoria auditoria, CancellationToken ct);

    /// <summary>Persiste los cambios de una auditoría ya guardada (cierre).</summary>
    Task ActualizarAsync(Domain.Auditoria auditoria, CancellationToken ct);

    Task<PaginaAuditoria> ConsultarAsync(FiltroAuditoria filtro, CancellationToken ct);
}

public sealed record FiltroAuditoria(
    int? EmpresaId, int? ActorPersonaId, DateTime? DesdeUtc, DateTime? HastaExclusivaUtc, int Pagina, int Tamano);

public sealed record AuditoriaDto(
    long Id, DateTime FechaUtc, int ActorPersonaId, string ActorNombre, string Accion,
    int? EmpresaId, int? UsuarioId, string Resultado, string? Detalle, string? Ip);

public sealed record PaginaAuditoria(int Total, int Pagina, int Tamano, IReadOnlyList<AuditoriaDto> Items);

/// <summary>
/// Envuelve cada acción del superadmin: escribe la auditoría en <c>pendiente</c> ANTES de llamar a Natural y
/// la cierra en <c>ok</c>/<c>error</c> después. Si no se puede escribir la intención, la acción NO se ejecuta.
/// </summary>
public sealed class AuditoriaServicio(IAuditoriaRepositorio repositorio, TimeProvider reloj)
{
    /// <summary>La operación recibe el Id de la auditoría ya guardada (p. ej. para enlazar una sesión de soporte).</summary>
    public async Task<T> EjecutarAsync<T>(
        Actor actor, string accion, int? empresaId, int? usuarioId, string? detalle,
        Func<long, Task<T>> operacion, CancellationToken ct)
    {
        var auditoria = Domain.Auditoria.Iniciar(
            reloj.GetUtcNow().UtcDateTime, actor.PersonaId, actor.Nombre, accion, empresaId, usuarioId, detalle, actor.Ip);

        await repositorio.AgregarAsync(auditoria, ct);

        try
        {
            var resultado = await operacion(auditoria.Id);
            auditoria.Cerrar(exito: true);
            await repositorio.ActualizarAsync(auditoria, ct);
            return resultado;
        }
        catch (Exception ex)
        {
            auditoria.Cerrar(exito: false, ex.Message);
            // El cierre no debe ocultar el error original de la operación.
            try { await repositorio.ActualizarAsync(auditoria, CancellationToken.None); } catch { /* queda 'pendiente' */ }
            throw;
        }
    }

    public Task<T> EjecutarAsync<T>(
        Actor actor, string accion, int? empresaId, int? usuarioId, string? detalle,
        Func<Task<T>> operacion, CancellationToken ct) =>
        EjecutarAsync(actor, accion, empresaId, usuarioId, detalle, _ => operacion(), ct);

    /// <summary>Variante sin valor de retorno.</summary>
    public Task EjecutarAsync(
        Actor actor, string accion, int? empresaId, int? usuarioId, string? detalle,
        Func<Task> operacion, CancellationToken ct) =>
        EjecutarAsync<bool>(actor, accion, empresaId, usuarioId, detalle, async _ => { await operacion(); return true; }, ct);
}
