namespace Brar.Domain;

/// <summary>Registro de una impersonación (superadmin "entra como" una empresa). Se usa desde F5.</summary>
public sealed class SesionSoporte
{
    public Guid Id { get; private set; }
    public long AuditoriaId { get; private set; }
    public int ActorPersonaId { get; private set; }
    public int EmpresaId { get; private set; }
    public string Motivo { get; private set; } = "";
    public DateTime CreadaUtc { get; private set; }
    public DateTime ExpiraUtc { get; private set; }

    private SesionSoporte() { }

    public static SesionSoporte Crear(
        long auditoriaId, int actorPersonaId, int empresaId, string motivo, DateTime ahoraUtc, DateTime expiraUtc)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length is < 5 or > 300)
            throw new ArgumentException("El motivo debe tener entre 5 y 300 caracteres.", nameof(motivo));
        if (expiraUtc <= ahoraUtc)
            throw new ArgumentException("La expiración debe ser posterior a la creación.", nameof(expiraUtc));

        return new SesionSoporte
        {
            Id = Guid.NewGuid(),
            AuditoriaId = auditoriaId,
            ActorPersonaId = actorPersonaId,
            EmpresaId = empresaId,
            Motivo = motivo.Trim(),
            CreadaUtc = ahoraUtc,
            ExpiraUtc = expiraUtc
        };
    }
}
