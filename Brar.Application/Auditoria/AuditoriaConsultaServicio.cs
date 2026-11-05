using Brar.Application.Common;

namespace Brar.Application.Auditoria;

public sealed class AuditoriaConsultaServicio(IAuditoriaRepositorio repositorio)
{
    public const int TamanoMaximo = 100;

    /// <param name="desde">Inclusive.</param>
    /// <param name="hasta">Si viene solo la fecha (sin hora) se incluye ese día completo.</param>
    public Task<PaginaAuditoria> ConsultarAsync(
        int? empresaId, int? actorPersonaId, DateTime? desde, DateTime? hasta, int pagina, int tamano, CancellationToken ct)
    {
        if (pagina < 1) throw ErrorApi.Invalido("La página debe ser mayor o igual a 1.");
        if (tamano is < 1 or > TamanoMaximo) throw ErrorApi.Invalido($"El tamaño debe estar entre 1 y {TamanoMaximo}.");
        if (desde is not null && hasta is not null && desde > hasta)
            throw ErrorApi.Invalido("'desde' no puede ser posterior a 'hasta'.");

        var desdeUtc = desde is null ? (DateTime?)null : DateTime.SpecifyKind(desde.Value, DateTimeKind.Utc);
        DateTime? hastaExclusiva = hasta is null
            ? null
            : DateTime.SpecifyKind(hasta.Value.TimeOfDay == TimeSpan.Zero ? hasta.Value.AddDays(1) : hasta.Value, DateTimeKind.Utc);

        return repositorio.ConsultarAsync(new FiltroAuditoria(empresaId, actorPersonaId, desdeUtc, hastaExclusiva, pagina, tamano), ct);
    }
}
