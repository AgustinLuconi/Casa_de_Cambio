using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaCambio.Server.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTipoPaseAMoneda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo_pase",
                table: "monedas",
                type: "text",
                nullable: false,
                defaultValue: "D");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tipo_pase",
                table: "monedas");
        }
    }
}
