using Brar.Application.Auditoria;
using Brar.Application.Soporte;
using Brar.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brar.Infrastructure.Persistencia;

internal sealed class AuditoriaRepositorio(BrarDbContext contexto) : IAuditoriaRepositorio
{
    public async Task AgregarAsync(Auditoria auditoria, CancellationToken ct)
    {
        contexto.Auditorias.Add(auditoria);
        await contexto.SaveChangesAsync(ct);
    }

    public async Task ActualizarAsync(Auditoria auditoria, CancellationToken ct)
    {
        // La entidad ya está rastreada por el contexto (mismo alcance de la petición).
        await contexto.SaveChangesAsync(ct);
    }

    public async Task<PaginaAuditoria> ConsultarAsync(FiltroAuditoria filtro, CancellationToken ct)
    {
        var consulta = contexto.Auditorias.AsNoTracking().AsQueryable();

        if (filtro.EmpresaId is int empresaId) consulta = consulta.Where(a => a.EmpresaId == empresaId);
        if (filtro.ActorPersonaId is int actorId) consulta = consulta.Where(a => a.ActorPersonaId == actorId);
        if (filtro.DesdeUtc is DateTime desde) consulta = consulta.Where(a => a.FechaUtc >= desde);
        if (filtro.HastaExclusivaUtc is DateTime hasta) consulta = consulta.Where(a => a.FechaUtc < hasta);

        var total = await consulta.CountAsync(ct);

        var filas = await consulta
            .OrderByDescending(a => a.FechaUtc).ThenByDescending(a => a.Id)
            .Skip((filtro.Pagina - 1) * filtro.Tamano)
            .Take(filtro.Tamano)
            .ToListAsync(ct);

        var items = filas.Select(a => new AuditoriaDto(
            a.Id, a.FechaUtc, a.ActorPersonaId, a.ActorNombre, a.Accion, a.EmpresaId, a.UsuarioId,
            a.Resultado.ToString().ToLowerInvariant(), a.Detalle, a.Ip)).ToList();

        return new PaginaAuditoria(total, filtro.Pagina, filtro.Tamano, items);
    }
}

internal sealed class SesionSoporteRepositorio(BrarDbContext contexto) : ISesionSoporteRepositorio
{
    public async Task AgregarAsync(SesionSoporte sesion, CancellationToken ct)
    {
        contexto.SesionesSoporte.Add(sesion);
        await contexto.SaveChangesAsync(ct);
    }
}
