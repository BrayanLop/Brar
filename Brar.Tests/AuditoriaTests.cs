using Brar.Application.Auditoria;
using Brar.Application.Common;
using Brar.Domain;

namespace Brar.Tests;

public class AuditoriaDominioTests
{
    private static readonly DateTime Ahora = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Iniciar_queda_pendiente_y_cerrar_ok()
    {
        var a = Auditoria.Iniciar(Ahora, 7, "Ana", "CrearEmpresa", null, null, "Empresa 'X'", "1.2.3.4");
        Assert.Equal(ResultadoAuditoria.Pendiente, a.Resultado);

        a.Cerrar(exito: true);
        Assert.Equal(ResultadoAuditoria.Ok, a.Resultado);
    }

    [Fact]
    public void Cerrar_dos_veces_falla()
    {
        var a = Auditoria.Iniciar(Ahora, 7, "Ana", "X", null, null, null, null);
        a.Cerrar(false, "boom");
        Assert.Equal(ResultadoAuditoria.Error, a.Resultado);
        Assert.Throws<InvalidOperationException>(() => a.Cerrar(true));
    }

    [Theory]
    [InlineData("la contraseña es 123")]
    [InlineData("Authorization: Bearer abc")]
    [InlineData("token=eyJ")]
    [InlineData("PASSWORD x")]
    public void Detalle_con_datos_sensibles_se_omite(string detalle)
    {
        var a = Auditoria.Iniciar(Ahora, 7, "Ana", "X", null, null, detalle, null);
        Assert.Equal(Auditoria.DetalleOmitido, a.Detalle);
    }

    [Fact]
    public void Detalle_largo_se_recorta()
    {
        var a = Auditoria.Iniciar(Ahora, 7, "Ana", "X", null, null, new string('a', 5000), null);
        Assert.Equal(Auditoria.LongitudMaximaDetalle, a.Detalle!.Length);
    }

    [Fact]
    public void Actor_invalido_o_sin_accion_lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Auditoria.Iniciar(Ahora, 0, "Ana", "X", null, null, null, null));
        Assert.Throws<ArgumentException>(() => Auditoria.Iniciar(Ahora, 7, "Ana", " ", null, null, null, null));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    public void SesionSoporte_exige_motivo_de_5_a_300(string motivo)
    {
        Assert.Throws<ArgumentException>(() => SesionSoporte.Crear(1, 7, 2, motivo, Ahora, Ahora.AddMinutes(30)));
    }

    [Fact]
    public void SesionSoporte_exige_expiracion_futura()
    {
        Assert.Throws<ArgumentException>(() => SesionSoporte.Crear(1, 7, 2, "Revisión de soporte", Ahora, Ahora));
    }
}

public class AuditoriaServicioTests
{
    private static readonly Actor Yo = new(7, "Ana", "127.0.0.1");

    private sealed class RepoFalso : IAuditoriaRepositorio
    {
        public List<Auditoria> Guardadas { get; } = [];
        public List<ResultadoAuditoria> EstadoAlGuardar { get; } = [];
        public bool FallarAlAgregar { get; set; }

        public Task<PaginaAuditoria> ConsultarAsync(FiltroAuditoria filtro, CancellationToken ct) => throw new NotImplementedException();

        public Task AgregarAsync(Auditoria auditoria, CancellationToken ct)
        {
            if (FallarAlAgregar) throw new InvalidOperationException("BD caída");
            Guardadas.Add(auditoria);
            EstadoAlGuardar.Add(auditoria.Resultado);
            return Task.CompletedTask;
        }

        public Task ActualizarAsync(Auditoria auditoria, CancellationToken ct)
        {
            EstadoAlGuardar.Add(auditoria.Resultado);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Escribe_pendiente_antes_de_ejecutar_y_cierra_ok()
    {
        var repo = new RepoFalso();
        var servicio = new AuditoriaServicio(repo, TimeProvider.System);
        var estadoDentro = (ResultadoAuditoria?)null;

        var valor = await servicio.EjecutarAsync(Yo, "CrearEmpresa", null, null, "d", () =>
        {
            estadoDentro = repo.Guardadas.Single().Resultado;
            return Task.FromResult(42);
        }, CancellationToken.None);

        Assert.Equal(42, valor);
        Assert.Equal(ResultadoAuditoria.Pendiente, estadoDentro);
        Assert.Equal([ResultadoAuditoria.Pendiente, ResultadoAuditoria.Ok], repo.EstadoAlGuardar);
    }

    [Fact]
    public async Task Si_la_operacion_falla_cierra_en_error_y_relanza()
    {
        var repo = new RepoFalso();
        var servicio = new AuditoriaServicio(repo, TimeProvider.System);

        var ex = await Assert.ThrowsAsync<ErrorApi>(() =>
            servicio.EjecutarAsync(Yo, "X", 1, null, null, () => throw ErrorApi.Invalido("nope"), CancellationToken.None));

        Assert.Equal(400, ex.Status);
        Assert.Equal(ResultadoAuditoria.Error, repo.Guardadas.Single().Resultado);
    }

    [Fact]
    public async Task Si_no_se_puede_auditar_la_operacion_no_se_ejecuta()
    {
        var repo = new RepoFalso { FallarAlAgregar = true };
        var servicio = new AuditoriaServicio(repo, TimeProvider.System);
        var ejecutada = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servicio.EjecutarAsync(Yo, "Impersonar", 1, null, null, () => { ejecutada = true; return Task.CompletedTask; }, CancellationToken.None));

        Assert.False(ejecutada);
    }
}
