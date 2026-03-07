using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaCambio.Migrations
{
    /// <inheritdoc />
    public partial class AddMonedaToMovimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "moneda",
                table: "movimientos",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "moneda",
                table: "movimientos");
        }
    }
}
