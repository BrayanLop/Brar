using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Brar.Application.Auditoria;
using Brar.Application.Common;
using Brar.Application.Natural;
using Brar.Configuracion;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Brar.Tests;

/// <summary>Pruebas de la API completa (auth + autorización) con Natural y la BD sustituidos por falsos.</summary>
public class SeguridadApiTests : IClassFixture<SeguridadApiTests.BrarFactory>
{
    private const string ClaveJwt = "clave-de-prueba-de-natural-con-mas-de-32-caracteres";

    public sealed class BrarFactory : WebApplicationFactory<Program>
    {
        public BrarFactory()
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Host=localhost;Database=x;Username=x;Password=x");
            Environment.SetEnvironmentVariable("NaturalJwt__Key", ClaveJwt);
            Environment.SetEnvironmentVariable("NaturalApi__BaseUrl", "https://natural.test/api");
            Environment.SetEnvironmentVariable("NaturalApi__ClaveServicio", new string('k', 40));
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<INaturalPlataforma>();
                s.AddScoped<INaturalPlataforma, NaturalVacio>();
                s.RemoveAll<IAuditoriaRepositorio>();
                s.AddScoped<IAuditoriaRepositorio, RepoNulo>();
            });
    }

    private sealed class NaturalVacio : INaturalPlataforma
    {
        public Task<IReadOnlyList<EmpresaDto>> ListarEmpresasAsync(Actor actor, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EmpresaDto>>([new EmpresaDto(1, "Spa", null, null, null, true, 1, 1, 1)]);
        public Task<EmpresaDto> CrearEmpresaAsync(Actor actor, CrearEmpresaSolicitud s, CancellationToken ct) => throw new NotImplementedException();
        public Task ActualizarEmpresaAsync(Actor actor, int id, EmpresaSolicitud s, CancellationToken ct) => throw new NotImplementedException();
        public Task CambiarEstadoEmpresaAsync(Actor actor, int id, bool a, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<AccesoDto>> ListarAccesosAsync(Actor actor, int id, CancellationToken ct) => throw new NotImplementedException();
        public Task<AccesoDto> CrearAccesoAsync(Actor actor, int id, CrearAccesoSolicitud s, CancellationToken ct) => throw new NotImplementedException();
        public Task CambiarEstadoAccesoAsync(Actor actor, int e, int u, bool a, CancellationToken ct) => throw new NotImplementedException();
        public Task CambiarRolAccesoAsync(Actor actor, int e, int u, string? r, CancellationToken ct) => throw new NotImplementedException();
        public Task RestablecerContrasenaAsync(Actor actor, int e, int u, string? c, CancellationToken ct) => throw new NotImplementedException();
        public Task EliminarAccesoAsync(Actor actor, int e, int u, CancellationToken ct) => throw new NotImplementedException();
        public Task<ImpersonacionDto> ImpersonarAsync(Actor actor, int e, int m, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class RepoNulo : IAuditoriaRepositorio
    {
        public Task AgregarAsync(Brar.Domain.Auditoria a, CancellationToken ct) => Task.CompletedTask;
        public Task ActualizarAsync(Brar.Domain.Auditoria a, CancellationToken ct) => Task.CompletedTask;
    }

    private readonly BrarFactory _factory;

    public SeguridadApiTests(BrarFactory factory) => _factory = factory;

    private static string Token(string? rol, string clave = ClaveJwt, string issuer = "Natural.Api",
        string audience = "Natural.Client", DateTime? vence = null, string? sub = "7")
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "Ana") };
        if (sub is not null) claims.Add(new Claim(JwtRegisteredClaimNames.Sub, sub));
        if (rol is not null) claims.Add(new Claim(ClaimTypes.Role, rol));

        var jwt = new JwtSecurityToken(issuer, audience, claims,
            expires: vence ?? DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private async Task<HttpStatusCode> Get(string ruta, string? token)
    {
        var cliente = _factory.CreateClient();
        if (token is not null) cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await cliente.GetAsync(ruta)).StatusCode;
    }

    [Fact] public async Task Health_es_publico() => Assert.Equal(HttpStatusCode.OK, await Get("/health", null));

    [Fact] public async Task Sin_token_es_401() => Assert.Equal(HttpStatusCode.Unauthorized, await Get("/Empresas", null));

    [Fact] public async Task Token_rol_00_pasa() => Assert.Equal(HttpStatusCode.OK, await Get("/Empresas", Token("00")));

    [Theory]
    [InlineData("01")]
    [InlineData("02")]
    [InlineData("03")]
    public async Task Otros_roles_son_403(string rol) =>
        Assert.Equal(HttpStatusCode.Forbidden, await Get("/Empresas", Token(rol)));

    [Fact] public async Task Token_sin_rol_es_403() => Assert.Equal(HttpStatusCode.Forbidden, await Get("/Empresas", Token(null)));

    [Fact] public async Task Firma_de_otra_clave_es_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await Get("/Empresas", Token("00", clave: "otra-clave-distinta-de-mas-de-32-caracteres!!")));

    [Fact] public async Task Issuer_distinto_es_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await Get("/Empresas", Token("00", issuer: "Citas")));

    [Fact] public async Task Audience_distinta_es_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await Get("/Empresas", Token("00", audience: "otro")));

    [Fact] public async Task Token_vencido_es_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await Get("/Empresas", Token("00", vence: DateTime.UtcNow.AddMinutes(-10))));

    [Fact] public async Task Token_sin_sub_valido_es_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await Get("/Empresas", Token("00", sub: "no-numero")));

    [Fact]
    public async Task Error_de_validacion_se_devuelve_como_400_con_el_mensaje()
    {
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("00"));

        var respuesta = await cliente.PostAsJsonAsync("/Empresas", new { nombre = " " });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("nombre de la empresa", cuerpo);
    }

    [Fact]
    public void Configuracion_falta_variables_y_las_nombra()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var ex = Assert.Throws<InvalidOperationException>(() => ConfiguracionRequerida.Validar(config));
        Assert.Contains("NaturalApi__ClaveServicio", ex.Message);
        Assert.Contains("NaturalJwt__Key", ex.Message);
        Assert.Contains("ConnectionStrings__DefaultConnection", ex.Message);
    }
}
