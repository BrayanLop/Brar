using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Brar.Application.Common;
using Brar.Application.Natural;
using Microsoft.Extensions.Logging;

namespace Brar.Infrastructure.Natural;

/// <summary>
/// Adaptador HTTP de <c>api/Plataforma/*</c> de Natural. Añade <c>X-Service-Key</c> (en el HttpClient) y
/// <c>X-Actor</c> (el superadmin) y traduce las fallas de Natural a <see cref="ErrorApi"/>:
/// 400/404/409 con su mensaje · 401/403/404 sin mensaje (clave mala o API deshabilitada) → 502 ·
/// timeout → 504 · sin conexión → 502. Nunca se loguean cuerpos de petición (llevan contraseñas).
/// </summary>
internal sealed class NaturalPlataformaHttp(HttpClient http, ILogger<NaturalPlataformaHttp> logger) : INaturalPlataforma
{
    private const string HeaderActor = "X-Actor";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<EmpresaDto>> ListarEmpresasAsync(Actor actor, CancellationToken ct) =>
        await Enviar<List<EmpresaDto>>(actor, HttpMethod.Get, "Plataforma/Empresas", null, ct) ?? [];

    public async Task<EmpresaDto> CrearEmpresaAsync(Actor actor, CrearEmpresaSolicitud solicitud, CancellationToken ct) =>
        await Enviar<EmpresaDto>(actor, HttpMethod.Post, "Plataforma/Empresas", solicitud, ct) ?? throw RespuestaVacia();

    public Task ActualizarEmpresaAsync(Actor actor, int empresaId, EmpresaSolicitud solicitud, CancellationToken ct) =>
        Enviar<object>(actor, HttpMethod.Put, $"Plataforma/Empresas/{empresaId}", solicitud, ct);

    public Task CambiarEstadoEmpresaAsync(Actor actor, int empresaId, bool activa, CancellationToken ct) =>
        Enviar<object>(actor, HttpMethod.Patch, $"Plataforma/Empresas/{empresaId}/Estado?activa={Bool(activa)}", null, ct);

    public async Task<IReadOnlyList<AccesoDto>> ListarAccesosAsync(Actor actor, int empresaId, CancellationToken ct) =>
        await Enviar<List<AccesoDto>>(actor, HttpMethod.Get, $"Plataforma/Empresas/{empresaId}/Accesos", null, ct) ?? [];

    public async Task<AccesoDto> CrearAccesoAsync(Actor actor, int empresaId, CrearAccesoSolicitud solicitud, CancellationToken ct) =>
        await Enviar<AccesoDto>(actor, HttpMethod.Post, $"Plataforma/Empresas/{empresaId}/Accesos", solicitud, ct) ?? throw RespuestaVacia();

    public Task CambiarEstadoAccesoAsync(Actor actor, int empresaId, int usuarioId, bool activo, CancellationToken ct) =>
        Enviar<object>(actor, HttpMethod.Patch, $"Plataforma/Empresas/{empresaId}/Accesos/{usuarioId}/Estado?activo={Bool(activo)}", null, ct);

    public Task CambiarRolAccesoAsync(Actor actor, int empresaId, int usuarioId, string? rol, CancellationToken ct) =>
        Enviar<object>(actor, HttpMethod.Patch, $"Plataforma/Empresas/{empresaId}/Accesos/{usuarioId}/Rol", new CambiarRolSolicitud(rol), ct);

    public Task RestablecerContrasenaAsync(Actor actor, int empresaId, int usuarioId, string? nuevaContrasena, CancellationToken ct) =>
        Enviar<object>(actor, HttpMethod.Put, $"Plataforma/Empresas/{empresaId}/Accesos/{usuarioId}/Contrasena",
            new RestablecerContrasenaSolicitud(nuevaContrasena), ct);

    public Task EliminarAccesoAsync(Actor actor, int empresaId, int usuarioId, CancellationToken ct) =>
        Enviar<object>(actor, HttpMethod.Delete, $"Plataforma/Empresas/{empresaId}/Accesos/{usuarioId}", null, ct);

    public async Task<ImpersonacionDto> ImpersonarAsync(Actor actor, int empresaId, int minutos, CancellationToken ct) =>
        await Enviar<ImpersonacionDto>(actor, HttpMethod.Post, $"Plataforma/Empresas/{empresaId}/Impersonar",
            new ImpersonarSolicitud($"{actor.PersonaId}:{actor.Nombre}", minutos), ct) ?? throw RespuestaVacia();

    private async Task<T?> Enviar<T>(Actor actor, HttpMethod metodo, string ruta, object? cuerpo, CancellationToken ct)
    {
        using var peticion = new HttpRequestMessage(metodo, ruta);
        peticion.Headers.Add(HeaderActor, actor.PersonaId.ToString());
        if (cuerpo is not null)
            peticion.Content = JsonContent.Create(cuerpo, options: Json);

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.SendAsync(peticion, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogError("Natural no respondió a tiempo: {Metodo} {Ruta}", metodo, ruta);
            throw new ErrorApi(StatusCodes504, "Natural no respondió a tiempo. Intenta de nuevo.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "No se pudo conectar con Natural: {Metodo} {Ruta}", metodo, ruta);
            throw new ErrorApi(StatusCodes502, "No se pudo conectar con Natural.", ex);
        }

        using (respuesta)
        {
            if (respuesta.IsSuccessStatusCode)
            {
                if (respuesta.StatusCode == HttpStatusCode.NoContent || respuesta.Content.Headers.ContentLength == 0)
                    return default;

                return typeof(T) == typeof(object)
                    ? default
                    : await respuesta.Content.ReadFromJsonAsync<T>(Json, ct);
            }

            var mensaje = (await respuesta.Content.ReadAsStringAsync(ct)).Trim().Trim('"');
            throw Traducir(respuesta.StatusCode, mensaje, metodo, ruta);
        }
    }

    private ErrorApi Traducir(HttpStatusCode status, string mensaje, HttpMethod metodo, string ruta)
    {
        switch (status)
        {
            case HttpStatusCode.BadRequest or HttpStatusCode.Conflict when mensaje.Length > 0:
                return new ErrorApi((int)status, mensaje);

            // 404 con texto = recurso inexistente; 404 vacío = la API de plataforma está deshabilitada en Natural.
            case HttpStatusCode.NotFound when mensaje.Length > 0:
                return new ErrorApi((int)status, mensaje);

            default:
                logger.LogError("Natural respondió {Status} a {Metodo} {Ruta}. Revisa Plataforma__ClaveServicio en Natural y NaturalApi__ClaveServicio aquí.",
                    (int)status, metodo, ruta);
                return new ErrorApi(StatusCodes502, "Natural rechazó la operación. Revisa la configuración del servicio.");
        }
    }

    private static ErrorApi RespuestaVacia() => new(StatusCodes502, "Natural devolvió una respuesta vacía.");

    private static string Bool(bool valor) => valor ? "true" : "false";

    private const int StatusCodes502 = 502;
    private const int StatusCodes504 = 504;
}
