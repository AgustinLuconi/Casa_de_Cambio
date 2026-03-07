using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SistemaCambio.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCierreCaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cierres_caja",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_cierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario = table.Column<string>(type: "text", nullable: false),
                    cantidad_compras = table.Column<int>(type: "integer", nullable: false),
                    total_compras_usd = table.Column<decimal>(type: "numeric", nullable: false),
                    total_compras_ars = table.Column<decimal>(type: "numeric", nullable: false),
                    cantidad_ventas = table.Column<int>(type: "integer", nullable: false),
                    total_ventas_usd = table.Column<decimal>(type: "numeric", nullable: false),
                    total_ventas_ars = table.Column<decimal>(type: "numeric", nullable: false),
                    saldo_caja_ars = table.Column<decimal>(type: "numeric", nullable: false),
                    saldo_caja_usd = table.Column<decimal>(type: "numeric", nullable: false),
                    saldo_caja_eur = table.Column<decimal>(type: "numeric", nullable: false),
                    total_diferencias = table.Column<decimal>(type: "numeric", nullable: false),
                    observaciones = table.Column<string>(type: "text", nullable: false),
                    cerrado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cierres_caja", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cierres_caja");
        }
    }
}
