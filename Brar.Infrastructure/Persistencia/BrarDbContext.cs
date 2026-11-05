using Brar.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brar.Infrastructure.Persistencia;

public class BrarDbContext(DbContextOptions<BrarDbContext> options) : DbContext(options)
{
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<SesionSoporte> SesionesSoporte => Set<SesionSoporte>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Auditoria>(e =>
        {
            e.ToTable("Auditoria");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.FechaUtc).IsRequired();
            e.Property(x => x.ActorNombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Accion).HasMaxLength(50).IsRequired();
            e.Property(x => x.Resultado).HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<ResultadoAuditoria>(v, ignoreCase: true)).HasMaxLength(10).IsRequired();
            e.Property(x => x.Detalle).HasMaxLength(Auditoria.LongitudMaximaDetalle);
            e.Property(x => x.Ip).HasMaxLength(45);
            e.HasIndex(x => new { x.EmpresaId, x.FechaUtc }).IsDescending(false, true).HasDatabaseName("IX_Auditoria_Empresa_Fecha");
            e.HasIndex(x => new { x.ActorPersonaId, x.FechaUtc }).IsDescending(false, true).HasDatabaseName("IX_Auditoria_Actor_Fecha");
        });

        modelBuilder.Entity<SesionSoporte>(e =>
        {
            e.ToTable("SesionSoporte");
            e.HasKey(x => x.Id);
            e.Property(x => x.Motivo).HasMaxLength(300).IsRequired();
            e.HasOne<Auditoria>().WithMany().HasForeignKey(x => x.AuditoriaId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.EmpresaId, x.ExpiraUtc }).HasDatabaseName("IX_SesionSoporte_Empresa_Expira");
        });
    }
}

/// <summary>Solo para <c>dotnet ef</c> (generar migraciones); no abre conexión.</summary>
public sealed class BrarDbContextFactory : IDesignTimeDbContextFactory<BrarDbContext>
{
    public BrarDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                       ?? "Host=localhost;Port=5432;Database=brar;Username=postgres;Password=design-time";
        var opciones = new DbContextOptionsBuilder<BrarDbContext>().UseNpgsql(conexion).Options;
        return new BrarDbContext(opciones);
    }
}
