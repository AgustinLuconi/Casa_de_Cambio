using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaCambio.Server.Migrations
{
    /// <inheritdoc />
    public partial class AgregarIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "operaciones",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_operaciones_idempotency_key",
                table: "operaciones",
                column: "idempotency_key",
                unique: true,
                filter: "idempotency_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_operaciones_idempotency_key",
                table: "operaciones");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "operaciones");
        }
    }
}
