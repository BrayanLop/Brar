using Microsoft.AspNetCore.Http;

namespace Brar.Application.Common;

/// <summary>Error de negocio o de integración con un status HTTP; el handler de la API lo traduce a ProblemDetails.</summary>
public sealed class ErrorApi : Exception
{
    public int Status { get; }

    public ErrorApi(int status, string mensaje, Exception? inner = null) : base(mensaje, inner) => Status = status;

    public static ErrorApi Invalido(string mensaje) => new(StatusCodes.Status400BadRequest, mensaje);
}

/// <summary>Superadmin que ejecuta la acción (sale del JWT de Natural).</summary>
public sealed record Actor(int PersonaId, string Nombre, string? Ip);
