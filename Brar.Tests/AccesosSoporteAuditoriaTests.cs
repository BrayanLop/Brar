using Brar.Application.Accesos;
using Brar.Application.Auditoria;
using Brar.Application.Common;
using Brar.Application.Natural;
using Brar.Application.Soporte;
using Brar.Domain;
using Brar.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Brar.Tests;

/// <summary>Natural falso configurable: registra llamadas y puede fallar.</summary>
internal sealed class NaturalConfigurable : INaturalPlataforma
{
    public List<string> Llamadas { get; } = [];
    public Exception? Falla { get; set; }
    public DateTime ExpiraEn { get; set; } = DateTime.UtcNow.AddMinutes(30);
    public CrearAccesoSolicitud? AccesoRecibido { get; private set; }

    private Task Registrar(string nombre)
    {
        Llamadas.Add(nombre);
        return Falla is null ? Task.CompletedTask : Task.FromException(Falla);
    }

    public Task<IReadOnlyList<EmpresaDto>> ListarEmpresasAsync(Actor a, CancellationToken ct) => throw new NotImplementedException();
    public Task<EmpresaDto> CrearEmpresaAsync(Actor a, CrearEmpresaSolicitud s, CancellationToken ct) => throw new NotImplementedException();
    public Task ActualizarEmpresaAsync(Actor a, int id, EmpresaSolicitud s, CancellationToken ct) => throw new NotImplementedException();
    public Task CambiarEstadoEmpresaAsync(Actor a, int id, bool x, CancellationToken ct) => throw new NotImplementedException();

    public async Task<IReadOnlyList<AccesoDto>> ListarAccesosAsync(Actor a, int e, CancellationToken ct)
    { await Registrar("Listar"); return []; }

    public async Task<AccesoDto> CrearAccesoAsync(Actor a, int e, CrearAccesoSolicitud s, CancellationToken ct)
    { AccesoRecibido = s; await Registrar("Crear"); return new AccesoDto(s.PersonaId, "N", "A", null, 9, s.NombreUsuario, true, true, s.Rol); }

    public Task CambiarEstadoAccesoAsync(Actor a, int e, int u, bool x, CancellationToken ct) => Registrar("Estado");
    public Task CambiarRolAccesoAsync(Actor a, int e, int u, string? r, CancellationToken ct) => Registrar("Rol");
    public Task RestablecerContrasenaAsync(Actor a, int e, int u, string? c, CancellationToken ct) => Registrar("Contrasena");
    public Task EliminarAccesoAsync(Actor a, int e, int u, CancellationToken ct) => Registrar("Eliminar");

    public async Task<ImpersonacionDto> ImpersonarAsync(Actor a, int e, int m, CancellationToken ct)
    {
        await Registrar("Impersonar");
        return new ImpersonacionDto("jwt-de-soporte", ExpiraEn, new PersonaSesionDto(1, 2, "Admin", e, "Spa", "01"));
    }
}

internal sealed class RepoAuditoriaMemoria : IAuditoriaRepositorio
{
    private long _id;
    public List<Auditoria> Items { get; } = [];

    public Task AgregarAsync(Auditoria a, CancellationToken ct)
    {
        // Simula el Id generado por la BD para poder enlazar la sesión de soporte.
        typeof(Auditoria).GetProperty(nameof(Auditoria.Id))!.SetValue(a, ++_id);
        Items.Add(a);
        return Task.CompletedTask;
    }

    public Task ActualizarAsync(Auditoria a, CancellationToken ct) => Task.CompletedTask;
    public Task<PaginaAuditoria> ConsultarAsync(FiltroAuditoria f, CancellationToken ct) => throw new NotImplementedException();
}

public class AccesosServicioTests
{
    private static readonly Actor Yo = new(7, "Ana", null);
    private static readonly string Hash = new('a', 44);

    private static (AccesosServicio S, NaturalConfigurable N, RepoAuditoriaMemoria R) Crear()
    {
        var n = new NaturalConfigurable();
        var r = new RepoAuditoriaMemoria();
        return (new AccesosServicio(n, new AuditoriaServicio(r, TimeProvider.System)), n, r);
    }

    [Fact]
    public async Task Crear_recorta_usuario_audita_sin_contrasena_y_llama_a_natural()
    {
        var (s, n, r) = Crear();

        await s.CrearAsync(Yo, 3, new CrearAccesoSolicitud(5, "  maria  ", Hash, "02"), CancellationToken.None);

        Assert.Equal("maria", n.AccesoRecibido!.NombreUsuario);
        var a = r.Items.Single();
        Assert.Equal(("CrearAcceso", 3, ResultadoAuditoria.Ok), (a.Accion, a.EmpresaId, a.Resultado));
        Assert.DoesNotContain(Hash, a.Detalle ?? "");
    }

    [Theory]
    [InlineData(0, "maria", 44, "02")]      // sin persona
    [InlineData(5, "ma", 44, "02")]         // usuario corto
    [InlineData(5, "ma ria", 44, "02")]     // con espacio
    [InlineData(5, "maria", 10, "02")]      // contraseña no es hash
    [InlineData(5, "maria", 44, "00")]      // el rol 00 no se asigna aquí
    [InlineData(5, "maria", 44, null)]
    public async Task Crear_invalido_responde_400_sin_llamar_a_natural(int persona, string usuario, int largoClave, string? rol)
    {
        var (s, n, r) = Crear();

        var ex = await Assert.ThrowsAsync<ErrorApi>(() =>
            s.CrearAsync(Yo, 3, new CrearAccesoSolicitud(persona, usuario, new string('a', largoClave), rol), CancellationToken.None));

        Assert.Equal(400, ex.Status);
        Assert.Empty(n.Llamadas);
        Assert.Empty(r.Items);
    }

    [Fact]
    public async Task Estado_audita_activar_y_desactivar()
    {
        var (s, _, r) = Crear();
        await s.CambiarEstadoAsync(Yo, 3, 9, true, CancellationToken.None);
        await s.CambiarEstadoAsync(Yo, 3, 9, false, CancellationToken.None);
        Assert.Equal(["ActivarAcceso", "DesactivarAcceso"], r.Items.Select(i => i.Accion));
        Assert.All(r.Items, i => Assert.Equal(9, i.UsuarioId));
    }

    [Theory]
    [InlineData("00")]
    [InlineData("99")]
    [InlineData(null)]
    public async Task CambiarRol_valida_el_rol(string? rol)
    {
        var (s, n, _) = Crear();
        await Assert.ThrowsAsync<ErrorApi>(() => s.CambiarRolAsync(Yo, 3, 9, new CambiarRolSolicitud(rol), CancellationToken.None));
        Assert.Empty(n.Llamadas);
    }

    [Fact]
    public async Task RestablecerContrasena_valida_formato_y_no_audita_la_clave()
    {
        var (s, n, r) = Crear();
        await Assert.ThrowsAsync<ErrorApi>(() =>
            s.RestablecerContrasenaAsync(Yo, 3, 9, new RestablecerContrasenaSolicitud("corta"), CancellationToken.None));

        await s.RestablecerContrasenaAsync(Yo, 3, 9, new RestablecerContrasenaSolicitud(Hash), CancellationToken.None);

        Assert.Equal(["Contrasena"], n.Llamadas);
        Assert.Null(r.Items.Single().Detalle);
    }

    [Fact]
    public async Task Rechazo_de_natural_deja_la_auditoria_en_error_y_se_propaga()
    {
        var (s, n, r) = Crear();
        n.Falla = new ErrorApi(400, "No se puede desactivar al único administrador activo de la empresa.");

        var ex = await Assert.ThrowsAsync<ErrorApi>(() => s.CambiarEstadoAsync(Yo, 3, 9, false, CancellationToken.None));

        Assert.Equal(400, ex.Status);
        Assert.Equal(ResultadoAuditoria.Error, r.Items.Single().Resultado);
    }

    [Fact]
    public async Task Eliminar_audita()
    {
        var (s, _, r) = Crear();
        await s.EliminarAsync(Yo, 3, 9, CancellationToken.None);
        Assert.Equal("EliminarAcceso", r.Items.Single().Accion);
    }
}

public class SoporteServicioTests
{
    private static readonly Actor Yo = new(7, "Ana", null);

    private sealed class SesionesMemoria : ISesionSoporteRepositorio
    {
        public List<SesionSoporte> Items { get; } = [];
        public bool Falla { get; set; }

        public Task AgregarAsync(SesionSoporte sesion, CancellationToken ct)
        {
            if (Falla) throw new InvalidOperationException("BD caída");
            Items.Add(sesion);
            return Task.CompletedTask;
        }
    }

    private static (SoporteServicio S, NaturalConfigurable N, RepoAuditoriaMemoria R, SesionesMemoria Se) Crear()
    {
        var n = new NaturalConfigurable();
        var r = new RepoAuditoriaMemoria();
        var se = new SesionesMemoria();
        return (new SoporteServicio(n, new AuditoriaServicio(r, TimeProvider.System), se, TimeProvider.System), n, r, se);
    }

    [Fact]
    public async Task Impersonar_audita_guarda_sesion_enlazada_y_devuelve_token()
    {
        var (s, n, r, se) = Crear();

        var res = await s.ImpersonarAsync(Yo, 4, new ImpersonarSolicitudBrar("Revisión del cierre de caja", 45), CancellationToken.None);

        Assert.Equal("jwt-de-soporte", res.Token);
        var auditoria = r.Items.Single();
        Assert.Equal(("Impersonar", 4, ResultadoAuditoria.Ok), (auditoria.Accion, auditoria.EmpresaId, auditoria.Resultado));
        var sesion = se.Items.Single();
        Assert.Equal(auditoria.Id, sesion.AuditoriaId);
        Assert.Equal((7, 4, "Revisión del cierre de caja"), (sesion.ActorPersonaId, sesion.EmpresaId, sesion.Motivo));
        Assert.Equal(["Impersonar"], n.Llamadas);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corto")]   // 5 caracteres: válido, se prueba abajo; este se queda en el borde inferior
    public async Task Motivo_minimo_5_caracteres(string? motivo)
    {
        var (s, n, r, se) = Crear();
        if (motivo == "corto")
        {
            await s.ImpersonarAsync(Yo, 4, new ImpersonarSolicitudBrar(motivo, null), CancellationToken.None);
            Assert.Single(se.Items);
            return;
        }

        await Assert.ThrowsAsync<ErrorApi>(() => s.ImpersonarAsync(Yo, 4, new ImpersonarSolicitudBrar(motivo, null), CancellationToken.None));
        Assert.Empty(n.Llamadas);
        Assert.Empty(r.Items);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(121)]
    public async Task Minutos_fuera_de_rango_son_400(int minutos)
    {
        var (s, n, _, _) = Crear();
        await Assert.ThrowsAsync<ErrorApi>(() => s.ImpersonarAsync(Yo, 4, new ImpersonarSolicitudBrar("Motivo válido", minutos), CancellationToken.None));
        Assert.Empty(n.Llamadas);
    }

    [Fact]
    public async Task Motivo_largo_es_400()
    {
        var (s, _, _, _) = Crear();
        await Assert.ThrowsAsync<ErrorApi>(() =>
            s.ImpersonarAsync(Yo, 4, new ImpersonarSolicitudBrar(new string('m', 301), null), CancellationToken.None));
    }

    [Fact]
    public async Task Si_natural_rechaza_no_hay_sesion_y_la_auditoria_queda_en_error()
    {
        var (s, n, r, se) = Crear();
        n.Falla = new ErrorApi(400, "La empresa está inactiva.");

        var ex = await Assert.ThrowsAsync<ErrorApi>(() =>
            s.ImpersonarAsync(Yo, 4, new ImpersonarSolicitudBrar("Revisión de soporte", null), CancellationToken.None));

        Assert.Equal("La empresa está inactiva.", ex.Message);
        Assert.Empty(se.Items);
        Assert.Equal(ResultadoAuditoria.Error, r.Items.Single().Resultado);
    }

    [Fact]
    public async Task Si_no_se_puede_guardar_la_sesion_el_token_no_se_devuelve()
    {
        var (s, _, r, se) = Crear();
        se.Falla = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.ImpersonarAsync(Yo, 4, new ImpersonarSolicitudBrar("Revisión de soporte", null), CancellationToken.None));

        Assert.Equal(ResultadoAuditoria.Error, r.Items.Single().Resultado);
    }
}

public class AuditoriaConsultaTests
{
    private static BrarDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<BrarDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(AuditoriaConsultaServicio S, BrarDbContext C)> Sembrar()
    {
        var c = NuevoContexto();
        var t0 = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        // 6 filas: empresa 1 (actor 7) x3, empresa 2 (actor 7) x2, empresa 2 (actor 8) x1, un día cada una.
        var datos = new[] { (1, 7), (1, 7), (1, 7), (2, 7), (2, 7), (2, 8) };
        for (var i = 0; i < datos.Length; i++)
        {
            var a = Auditoria.Iniciar(t0.AddDays(i), datos[i].Item2, "A", "Accion" + i, datos[i].Item1, null, null, null);
            a.Cerrar(true);
            c.Auditorias.Add(a);
        }
        await c.SaveChangesAsync();
        var repo = new AuditoriaRepositorio(c);
        return (new AuditoriaConsultaServicio(repo), c);
    }

    [Fact]
    public async Task Sin_filtros_pagina_y_ordena_por_fecha_descendente()
    {
        var (s, _) = await Sembrar();

        var p1 = await s.ConsultarAsync(null, null, null, null, 1, 4, CancellationToken.None);
        var p2 = await s.ConsultarAsync(null, null, null, null, 2, 4, CancellationToken.None);

        Assert.Equal(6, p1.Total);
        Assert.Equal(4, p1.Items.Count);
        Assert.Equal(2, p2.Items.Count);
        Assert.Equal("Accion5", p1.Items[0].Accion);
        Assert.Equal("Accion0", p2.Items[^1].Accion);
    }

    [Fact]
    public async Task Filtra_por_empresa_y_por_actor()
    {
        var (s, _) = await Sembrar();

        Assert.Equal(3, (await s.ConsultarAsync(1, null, null, null, 1, 25, CancellationToken.None)).Total);
        Assert.Equal(1, (await s.ConsultarAsync(null, 8, null, null, 1, 25, CancellationToken.None)).Total);
        Assert.Equal(2, (await s.ConsultarAsync(2, 7, null, null, 1, 25, CancellationToken.None)).Total);
    }

    [Fact]
    public async Task Hasta_solo_fecha_incluye_el_dia_completo()
    {
        var (s, _) = await Sembrar();
        // Hay filas el 1, 2, 3, 4, 5 y 6 de noviembre a las 10:00.
        var r = await s.ConsultarAsync(null, null, new DateTime(2026, 11, 2), new DateTime(2026, 11, 3), 1, 25, CancellationToken.None);
        Assert.Equal(2, r.Total);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Paginacion_invalida_es_400(int pagina, int tamano)
    {
        var (s, _) = await Sembrar();
        await Assert.ThrowsAsync<ErrorApi>(() => s.ConsultarAsync(null, null, null, null, pagina, tamano, CancellationToken.None));
    }

    [Fact]
    public async Task Desde_posterior_a_hasta_es_400()
    {
        var (s, _) = await Sembrar();
        await Assert.ThrowsAsync<ErrorApi>(() =>
            s.ConsultarAsync(null, null, new DateTime(2026, 12, 1), new DateTime(2026, 11, 1), 1, 25, CancellationToken.None));
    }
}
