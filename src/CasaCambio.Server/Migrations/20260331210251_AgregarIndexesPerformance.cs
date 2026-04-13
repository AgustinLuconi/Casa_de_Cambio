using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaCambio.Server.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIndexesPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_movimientos_cuenta_id",
                table: "movimientos");

            migrationBuilder.CreateIndex(
                name: "IX_operaciones_fecha",
                table: "operaciones",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "IX_operaciones_tipo_operacion_fecha",
                table: "operaciones",
                columns: new[] { "tipo_operacion", "fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_cuenta_id_fecha",
                table: "movimientos",
                columns: new[] { "cuenta_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_fecha",
                table: "movimientos",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "IX_cierres_caja_fecha",
                table: "cierres_caja",
                column: "fecha");

            migrationBuilder.CreateIndex(
                name: "IX_arqueos_fecha",
                table: "arqueos",
                column: "fecha");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_operaciones_fecha",
                table: "operaciones");

            migrationBuilder.DropIndex(
                name: "IX_operaciones_tipo_operacion_fecha",
                table: "operaciones");

            migrationBuilder.DropIndex(
                name: "IX_movimientos_cuenta_id_fecha",
                table: "movimientos");

            migrationBuilder.DropIndex(
                name: "IX_movimientos_fecha",
                table: "movimientos");

            migrationBuilder.DropIndex(
                name: "IX_cierres_caja_fecha",
                table: "cierres_caja");

            migrationBuilder.DropIndex(
                name: "IX_arqueos_fecha",
                table: "arqueos");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_cuenta_id",
                table: "movimientos",
                column: "cuenta_id");
        }
    }
}
