using Brar.Application.Auditoria;
using Brar.Application.Common;
using Brar.Application.Natural;
using Brar.Domain;

namespace Brar.Application.Soporte;

public interface ISesionSoporteRepositorio
{
    Task AgregarAsync(SesionSoporte sesion, CancellationToken ct);
}

public sealed record ImpersonarSolicitudBrar(string? Motivo, int? Minutos);

/// <summary>
/// "Entrar como" una empresa. La auditoría se escribe ANTES de pedir el token (si no se puede auditar, no se
/// emite) y la sesión de soporte queda enlazada a ella. El token solo se devuelve si todo quedó registrado.
/// </summary>
public sealed class SoporteServicio(
    INaturalPlataforma natural, AuditoriaServicio auditoria, ISesionSoporteRepositorio sesiones, TimeProvider reloj)
{
    public const int MinutosPorDefecto = 30;

    public Task<ImpersonacionDto> ImpersonarAsync(Actor actor, int empresaId, ImpersonarSolicitudBrar? solicitud, CancellationToken ct)
    {
        var motivo = (solicitud?.Motivo ?? "").Trim();
        if (motivo.Length is < 5 or > 300)
            throw ErrorApi.Invalido("El motivo es obligatorio (entre 5 y 300 caracteres).");

        var minutos = solicitud?.Minutos ?? MinutosPorDefecto;
        if (minutos is < 5 or > 120)
            throw ErrorApi.Invalido("Los minutos deben estar entre 5 y 120.");

        return auditoria.EjecutarAsync(actor, "Impersonar", empresaId, null, $"Motivo: {motivo}; {minutos} min",
            async auditoriaId =>
            {
                var resultado = await natural.ImpersonarAsync(actor, empresaId, minutos, ct);

                var sesion = SesionSoporte.Crear(
                    auditoriaId, actor.PersonaId, empresaId, motivo,
                    reloj.GetUtcNow().UtcDateTime, resultado.ExpiraEn.ToUniversalTime());
                await sesiones.AgregarAsync(sesion, ct);

                return resultado;
            }, ct);
    }
}
