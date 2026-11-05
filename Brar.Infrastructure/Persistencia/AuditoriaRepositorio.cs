using Brar.Application.Auditoria;
using Brar.Domain;

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
}
