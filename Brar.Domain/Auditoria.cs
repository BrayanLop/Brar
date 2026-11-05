namespace Brar.Domain;

public enum ResultadoAuditoria
{
    Pendiente,
    Ok,
    Error
}

/// <summary>
/// Rastro de una acción del superadmin. Se crea en <see cref="ResultadoAuditoria.Pendiente"/> ANTES de
/// llamar a Natural y se cierra después: así una acción real nunca queda sin rastro aunque el servicio caiga.
/// </summary>
public sealed class Auditoria
{
    public const int LongitudMaximaDetalle = 1000;
    public const string DetalleOmitido = "[detalle omitido: contenía datos sensibles]";

    private static readonly string[] PalabrasSensibles = ["contrasena", "contraseña", "password", "token", "clave", "secret", "bearer"];

    public long Id { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public int ActorPersonaId { get; private set; }
    public string ActorNombre { get; private set; } = "";
    public string Accion { get; private set; } = "";
    public int? EmpresaId { get; private set; }
    public int? UsuarioId { get; private set; }
    public ResultadoAuditoria Resultado { get; private set; }
    public string? Detalle { get; private set; }
    public string? Ip { get; private set; }

    private Auditoria() { }

    public static Auditoria Iniciar(
        DateTime ahoraUtc, int actorPersonaId, string actorNombre, string accion,
        int? empresaId, int? usuarioId, string? detalle, string? ip)
    {
        if (actorPersonaId <= 0) throw new ArgumentOutOfRangeException(nameof(actorPersonaId));
        if (string.IsNullOrWhiteSpace(accion)) throw new ArgumentException("La acción es obligatoria.", nameof(accion));

        return new Auditoria
        {
            FechaUtc = ahoraUtc,
            ActorPersonaId = actorPersonaId,
            ActorNombre = Recortar(string.IsNullOrWhiteSpace(actorNombre) ? "desconocido" : actorNombre, 150)!,
            Accion = Recortar(accion, 50)!,
            EmpresaId = empresaId,
            UsuarioId = usuarioId,
            Resultado = ResultadoAuditoria.Pendiente,
            Detalle = Sanear(detalle),
            Ip = Recortar(ip, 45)
        };
    }

    /// <summary>Cierra la auditoría. Si ya estaba cerrada no se puede volver a cambiar.</summary>
    public void Cerrar(bool exito, string? detalle = null)
    {
        if (Resultado != ResultadoAuditoria.Pendiente)
            throw new InvalidOperationException("La auditoría ya está cerrada.");

        Resultado = exito ? ResultadoAuditoria.Ok : ResultadoAuditoria.Error;

        var extra = Sanear(detalle);
        if (!string.IsNullOrEmpty(extra))
            Detalle = Sanear(string.IsNullOrEmpty(Detalle) ? extra : $"{Detalle} | {extra}");
    }

    /// <summary>Defensa en profundidad: nunca se guardan contraseñas, tokens ni claves en el detalle.</summary>
    internal static string? Sanear(string? detalle)
    {
        if (string.IsNullOrWhiteSpace(detalle)) return null;
        if (PalabrasSensibles.Any(p => detalle.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return DetalleOmitido;
        return Recortar(detalle.Trim(), LongitudMaximaDetalle);
    }

    private static string? Recortar(string? valor, int max) =>
        valor is null ? null : valor.Length <= max ? valor : valor[..max];
}
