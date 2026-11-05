using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Brar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    FechaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorPersonaId = table.Column<int>(type: "integer", nullable: false),
                    ActorNombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Accion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EmpresaId = table.Column<int>(type: "integer", nullable: true),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    Resultado = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Detalle = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SesionSoporte",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditoriaId = table.Column<long>(type: "bigint", nullable: false),
                    ActorPersonaId = table.Column<int>(type: "integer", nullable: false),
                    EmpresaId = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreadaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionSoporte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SesionSoporte_Auditoria_AuditoriaId",
                        column: x => x.AuditoriaId,
                        principalTable: "Auditoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Actor_Fecha",
                table: "Auditoria",
                columns: new[] { "ActorPersonaId", "FechaUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Empresa_Fecha",
                table: "Auditoria",
                columns: new[] { "EmpresaId", "FechaUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SesionSoporte_AuditoriaId",
                table: "SesionSoporte",
                column: "AuditoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionSoporte_Empresa_Expira",
                table: "SesionSoporte",
                columns: new[] { "EmpresaId", "ExpiraUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SesionSoporte");

            migrationBuilder.DropTable(
                name: "Auditoria");
        }
    }
}
