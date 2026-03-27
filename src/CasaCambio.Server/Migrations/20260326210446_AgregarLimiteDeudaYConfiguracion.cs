using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaCambio.Server.Migrations
{
    /// <inheritdoc />
    public partial class AgregarLimiteDeudaYConfiguracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "limite_deuda",
                table: "cuentas",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "configuracion_sistema",
                columns: table => new
                {
                    clave = table.Column<string>(type: "text", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_sistema", x => x.clave);
                });

            migrationBuilder.InsertData(
                table: "configuracion_sistema",
                columns: new[] { "clave", "valor", "descripcion" },
                values: new object[] { "limite_deuda_general", "0",
                    "Límite de deuda general para clientes sin límite específico. 0 = sin límite." });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "limite_deuda",
                table: "cuentas");
        }
    }
}
