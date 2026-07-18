using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaCambio.Server.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOperacionParejaId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "operacion_pareja_id",
                table: "operaciones",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_operaciones_operacion_pareja_id",
                table: "operaciones",
                column: "operacion_pareja_id");

            migrationBuilder.AddForeignKey(
                name: "FK_operaciones_operaciones_operacion_pareja_id",
                table: "operaciones",
                column: "operacion_pareja_id",
                principalTable: "operaciones",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_operaciones_operaciones_operacion_pareja_id",
                table: "operaciones");

            migrationBuilder.DropIndex(
                name: "IX_operaciones_operacion_pareja_id",
                table: "operaciones");

            migrationBuilder.DropColumn(
                name: "operacion_pareja_id",
                table: "operaciones");
        }
    }
}
