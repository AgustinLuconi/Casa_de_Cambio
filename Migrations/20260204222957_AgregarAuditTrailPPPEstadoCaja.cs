using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SistemaCambio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAuditTrailPPPEstadoCaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_id = table.Column<int>(type: "integer", nullable: true),
                    usuario_nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    accion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    entidad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entidad_id = table.Column<int>(type: "integer", nullable: false),
                    valores_anteriores = table.Column<string>(type: "text", nullable: true),
                    valores_nuevos = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "estados_caja",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cuenta_id = table.Column<int>(type: "integer", nullable: false),
                    fecha_apertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    saldo_apertura = table.Column<decimal>(type: "numeric", nullable: false),
                    fecha_cierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    saldo_cierre = table.Column<decimal>(type: "numeric", nullable: true),
                    arqueo_id = table.Column<int>(type: "integer", nullable: true),
                    estado = table.Column<string>(type: "text", nullable: false),
                    usuario_apertura = table.Column<string>(type: "text", nullable: true),
                    usuario_cierre = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estados_caja", x => x.id);
                    table.ForeignKey(
                        name: "FK_estados_caja_arqueos_arqueo_id",
                        column: x => x.arqueo_id,
                        principalTable: "arqueos",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_estados_caja_cuentas_cuenta_id",
                        column: x => x.cuenta_id,
                        principalTable: "cuentas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenencias_moneda",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    moneda_id = table.Column<int>(type: "integer", nullable: false),
                    cantidad_total = table.Column<decimal>(type: "numeric", nullable: false),
                    costo_total = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenencias_moneda", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenencias_moneda_monedas_moneda_id",
                        column: x => x.moneda_id,
                        principalTable: "monedas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_estados_caja_arqueo_id",
                table: "estados_caja",
                column: "arqueo_id");

            migrationBuilder.CreateIndex(
                name: "IX_estados_caja_cuenta_id",
                table: "estados_caja",
                column: "cuenta_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenencias_moneda_moneda_id",
                table: "tenencias_moneda",
                column: "moneda_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "estados_caja");

            migrationBuilder.DropTable(
                name: "tenencias_moneda");
        }
    }
}
