using System.Net;
using System.Text;
using Brar.Application.Common;
using Brar.Infrastructure.Natural;
using Microsoft.Extensions.Logging.Abstractions;

namespace Brar.Tests;

public class NaturalPlataformaHttpTests
{
    private static readonly Actor Yo = new(7, "Ana", null);

    private sealed class ManejadorFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Ultima { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Ultima = request;
            return Task.FromResult(responder(request));
        }
    }

    private static (NaturalPlataformaHttp Cliente, ManejadorFalso Manejador) Crear(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var manejador = new ManejadorFalso(responder);
        var http = new HttpClient(manejador) { BaseAddress = new Uri("https://natural.test/api/") };
        return (new NaturalPlataformaHttp(http, NullLogger<NaturalPlataformaHttp>.Instance), manejador);
    }

    private static HttpResponseMessage Texto(HttpStatusCode status, string cuerpo) =>
        new(status) { Content = new StringContent(cuerpo, Encoding.UTF8, "text/plain") };

    [Fact]
    public async Task Listar_envia_X_Actor_y_deserializa_camelCase()
    {
        var json = """[{"id":1,"nombre":"Spa","nit":null,"direccion":null,"telefono":null,"activa":true,"totalPersonas":3,"totalAccesos":2,"accesosActivos":2}]""";
        var (cliente, manejador) = Crear(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var empresas = await cliente.ListarEmpresasAsync(Yo, CancellationToken.None);

        Assert.Equal("Spa", empresas.Single().Nombre);
        Assert.Equal(3, empresas.Single().TotalPersonas);
        Assert.Equal("7", manejador.Ultima!.Headers.GetValues("X-Actor").Single());
        Assert.Equal("https://natural.test/api/Plataforma/Empresas", manejador.Ultima.RequestUri!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.Conflict, 409)]
    [InlineData(HttpStatusCode.NotFound, 404)]
    public async Task Errores_de_negocio_se_propagan_con_su_mensaje(HttpStatusCode status, int esperado)
    {
        var (cliente, _) = Crear(_ => Texto(status, "\"Ese nombre de usuario ya está en uso.\""));

        var ex = await Assert.ThrowsAsync<ErrorApi>(() => cliente.CambiarEstadoEmpresaAsync(Yo, 1, true, CancellationToken.None));

        Assert.Equal(esperado, ex.Status);
        Assert.Equal("Ese nombre de usuario ya está en uso.", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]   // clave de servicio inválida
    [InlineData(HttpStatusCode.NotFound)]       // API de plataforma deshabilitada (404 sin cuerpo)
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Fallas_de_configuracion_o_servidor_son_502(HttpStatusCode status)
    {
        var (cliente, _) = Crear(_ => Texto(status, ""));

        var ex = await Assert.ThrowsAsync<ErrorApi>(() => cliente.ListarEmpresasAsync(Yo, CancellationToken.None));

        Assert.Equal(502, ex.Status);
    }

    [Fact]
    public async Task Sin_conexion_es_502()
    {
        var (cliente, _) = Crear(_ => throw new HttpRequestException("sin red"));
        var ex = await Assert.ThrowsAsync<ErrorApi>(() => cliente.ListarEmpresasAsync(Yo, CancellationToken.None));
        Assert.Equal(502, ex.Status);
    }

    [Fact]
    public async Task Timeout_es_504()
    {
        var (cliente, _) = Crear(_ => throw new TaskCanceledException("timeout"));
        var ex = await Assert.ThrowsAsync<ErrorApi>(() => cliente.ListarEmpresasAsync(Yo, CancellationToken.None));
        Assert.Equal(504, ex.Status);
    }

    [Fact]
    public async Task Cancelacion_del_llamador_no_se_convierte_en_504()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (cliente, _) = Crear(_ => throw new TaskCanceledException("cancelado"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cliente.ListarEmpresasAsync(Yo, cts.Token));
    }

    [Fact]
    public async Task Estado_de_acceso_usa_query_y_metodo_patch()
    {
        var (cliente, manejador) = Crear(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"activo":false}""", Encoding.UTF8, "application/json")
        });

        await cliente.CambiarEstadoAccesoAsync(Yo, 2, 9, false, CancellationToken.None);

        Assert.Equal(HttpMethod.Patch, manejador.Ultima!.Method);
        Assert.EndsWith("Plataforma/Empresas/2/Accesos/9/Estado?activo=false", manejador.Ultima.RequestUri!.ToString());
    }
}
