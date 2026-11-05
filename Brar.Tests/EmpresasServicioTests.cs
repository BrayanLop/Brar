using Brar.Application.Auditoria;
using Brar.Application.Common;
using Brar.Application.Empresas;
using Brar.Application.Natural;
using Brar.Domain;

namespace Brar.Tests;

public class EmpresasServicioTests
{
    private static readonly Actor Yo = new(7, "Ana", null);
    private static readonly string Hash44 = new('a', 44);

    private sealed class NaturalFalso : INaturalPlataforma
    {
        public CrearEmpresaSolicitud? Recibida { get; private set; }
        public int Llamadas { get; private set; }

        public Task<IReadOnlyList<EmpresaDto>> ListarEmpresasAsync(Actor actor, CancellationToken ct) { Llamadas++; return Task.FromResult<IReadOnlyList<EmpresaDto>>([]); }
        public Task<EmpresaDto> CrearEmpresaAsync(Actor actor, CrearEmpresaSolicitud s, CancellationToken ct)
        {
            Llamadas++; Recibida = s;
            return Task.FromResult(new EmpresaDto(5, s.Nombre, null, null, null, true, 1, 1, 1));
        }
        public Task ActualizarEmpresaAsync(Actor actor, int id, EmpresaSolicitud s, CancellationToken ct) { Llamadas++; return Task.CompletedTask; }
        public Task CambiarEstadoEmpresaAsync(Actor actor, int id, bool a, CancellationToken ct) { Llamadas++; return Task.CompletedTask; }
        public Task<IReadOnlyList<AccesoDto>> ListarAccesosAsync(Actor actor, int id, CancellationToken ct) => throw new NotImplementedException();
        public Task<AccesoDto> CrearAccesoAsync(Actor actor, int id, CrearAccesoSolicitud s, CancellationToken ct) => throw new NotImplementedException();
        public Task CambiarEstadoAccesoAsync(Actor actor, int e, int u, bool a, CancellationToken ct) => throw new NotImplementedException();
        public Task CambiarRolAccesoAsync(Actor actor, int e, int u, string? r, CancellationToken ct) => throw new NotImplementedException();
        public Task RestablecerContrasenaAsync(Actor actor, int e, int u, string? c, CancellationToken ct) => throw new NotImplementedException();
        public Task EliminarAccesoAsync(Actor actor, int e, int u, CancellationToken ct) => throw new NotImplementedException();
        public Task<ImpersonacionDto> ImpersonarAsync(Actor actor, int e, int m, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class RepoMemoria : IAuditoriaRepositorio
    {
        public List<Auditoria> Items { get; } = [];
        public Task AgregarAsync(Auditoria a, CancellationToken ct) { Items.Add(a); return Task.CompletedTask; }
        public Task ActualizarAsync(Auditoria a, CancellationToken ct) => Task.CompletedTask;
        public Task<PaginaAuditoria> ConsultarAsync(FiltroAuditoria filtro, CancellationToken ct) => throw new NotImplementedException();
    }

    private static (EmpresasServicio Servicio, NaturalFalso Natural, RepoMemoria Repo) Crear()
    {
        var natural = new NaturalFalso();
        var repo = new RepoMemoria();
        return (new EmpresasServicio(natural, new AuditoriaServicio(repo, TimeProvider.System)), natural, repo);
    }

    private static CrearEmpresaSolicitud Valida() => new(
        " Spa Luna ", "900", null, null,
        new AdminEmpresaSolicitud("Luz", "Rojas", "123456", "l@x.com", "3001234567", "  luz.admin ", new string('a', 44)));

    [Fact]
    public async Task Crear_valida_recorta_usuario_audita_y_llama_a_natural()
    {
        var (servicio, natural, repo) = Crear();

        var empresa = await servicio.CrearAsync(Yo, Valida(), CancellationToken.None);

        Assert.Equal(5, empresa.Id);
        Assert.Equal("luz.admin", natural.Recibida!.Admin!.NombreUsuario);
        var auditoria = repo.Items.Single();
        Assert.Equal("CrearEmpresa", auditoria.Accion);
        Assert.Equal(ResultadoAuditoria.Ok, auditoria.Resultado);
        Assert.DoesNotContain(new string('a', 44), auditoria.Detalle ?? "");
    }

    [Theory]
    [InlineData("contrasena corta")]
    [InlineData("usuario con espacio")]
    [InlineData("sin nombre")]
    public async Task Crear_con_datos_invalidos_responde_400_y_no_llama_a_natural(string caso)
    {
        var (servicio, natural, repo) = Crear();
        var s = Valida();
        s = caso switch
        {
            "contrasena corta" => s with { Admin = s.Admin! with { Contrasena = "corta" } },
            "usuario con espacio" => s with { Admin = s.Admin! with { NombreUsuario = "a b c" } },
            _ => s with { Nombre = " " }
        };

        var ex = await Assert.ThrowsAsync<ErrorApi>(() => servicio.CrearAsync(Yo, s, CancellationToken.None));

        Assert.Equal(400, ex.Status);
        Assert.Equal(0, natural.Llamadas);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Crear_sin_admin_o_nulo_responde_400()
    {
        var (servicio, _, _) = Crear();
        await Assert.ThrowsAsync<ErrorApi>(() => servicio.CrearAsync(Yo, null, CancellationToken.None));
        await Assert.ThrowsAsync<ErrorApi>(() => servicio.CrearAsync(Yo, Valida() with { Admin = null }, CancellationToken.None));
    }

    [Theory]
    [InlineData(true, "ActivarEmpresa")]
    [InlineData(false, "DesactivarEmpresa")]
    public async Task CambiarEstado_audita_la_accion_correcta(bool activa, string accion)
    {
        var (servicio, _, repo) = Crear();
        await servicio.CambiarEstadoAsync(Yo, 3, activa, CancellationToken.None);
        Assert.Equal(accion, repo.Items.Single().Accion);
        Assert.Equal(3, repo.Items.Single().EmpresaId);
    }

    [Fact]
    public async Task Actualizar_valida_largos()
    {
        var (servicio, natural, _) = Crear();
        await Assert.ThrowsAsync<ErrorApi>(() =>
            servicio.ActualizarAsync(Yo, 1, new EmpresaSolicitud("Ok", new string('9', 21), null, null), CancellationToken.None));
        Assert.Equal(0, natural.Llamadas);
    }

    [Fact]
    public async Task Listar_no_audita()
    {
        var (servicio, natural, repo) = Crear();
        await servicio.ListarAsync(Yo, CancellationToken.None);
        Assert.Equal(1, natural.Llamadas);
        Assert.Empty(repo.Items);
    }
}
