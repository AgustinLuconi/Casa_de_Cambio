using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SistemaCambio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarMonedaCotizacionArqueo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arqueos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cuenta_id = table.Column<int>(type: "integer", nullable: false),
                    saldo_sistema = table.Column<decimal>(type: "numeric", nullable: false),
                    saldo_arqueo = table.Column<decimal>(type: "numeric", nullable: false),
                    diferencia = table.Column<decimal>(type: "numeric", nullable: false),
                    movimiento_ajuste_id = table.Column<int>(type: "integer", nullable: true),
                    observaciones = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arqueos", x => x.id);
                    table.ForeignKey(
                        name: "FK_arqueos_cuentas_cuenta_id",
                        column: x => x.cuenta_id,
                        principalTable: "cuentas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_arqueos_movimientos_movimiento_ajuste_id",
                        column: x => x.movimiento_ajuste_id,
                        principalTable: "movimientos",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "monedas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monedas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cotizaciones_diarias",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    moneda_id = table.Column<int>(type: "integer", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cotizacion_compra = table.Column<decimal>(type: "numeric", nullable: false),
                    cotizacion_venta = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cotizaciones_diarias", x => x.id);
                    table.ForeignKey(
                        name: "FK_cotizaciones_diarias_monedas_moneda_id",
                        column: x => x.moneda_id,
                        principalTable: "monedas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_arqueos_cuenta_id",
                table: "arqueos",
                column: "cuenta_id");

            migrationBuilder.CreateIndex(
                name: "IX_arqueos_movimiento_ajuste_id",
                table: "arqueos",
                column: "movimiento_ajuste_id");

            migrationBuilder.CreateIndex(
                name: "IX_cotizaciones_diarias_moneda_id",
                table: "cotizaciones_diarias",
                column: "moneda_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arqueos");

            migrationBuilder.DropTable(
                name: "cotizaciones_diarias");

            migrationBuilder.DropTable(
                name: "monedas");
        }
    }
}
