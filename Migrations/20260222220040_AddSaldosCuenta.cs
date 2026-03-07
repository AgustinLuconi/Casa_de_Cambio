using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SistemaCambio.Migrations
{
    /// <inheritdoc />
    public partial class AddSaldosCuenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saldos_cuenta",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cuenta_id = table.Column<int>(type: "integer", nullable: false),
                    moneda = table.Column<string>(type: "text", nullable: false),
                    saldo = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saldos_cuenta", x => x.id);
                    table.ForeignKey(
                        name: "FK_saldos_cuenta_cuentas_cuenta_id",
                        column: x => x.cuenta_id,
                        principalTable: "cuentas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saldos_cuenta_cuenta_id_moneda",
                table: "saldos_cuenta",
                columns: new[] { "cuenta_id", "moneda" },
                unique: true);

            // Migrar datos de cuentas -> saldos_cuenta
            migrationBuilder.Sql(@"
                INSERT INTO saldos_cuenta (cuenta_id, moneda, saldo)
                SELECT id, moneda, saldo FROM cuentas WHERE moneda IS NOT NULL AND moneda != '';
            ");

            migrationBuilder.DropColumn(
                name: "moneda",
                table: "cuentas");

            migrationBuilder.DropColumn(
                name: "saldo",
                table: "cuentas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "moneda",
                table: "cuentas",
                type: "text",
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<decimal>(
                name: "saldo",
                table: "cuentas",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            // Restaurar datos a la vieja columna desde el primer saldo
            migrationBuilder.Sql(@"
                UPDATE cuentas c
                SET 
                    moneda = s.moneda,
                    saldo = s.saldo
                FROM (
                    SELECT DISTINCT ON (cuenta_id) cuenta_id, moneda, saldo 
                    FROM saldos_cuenta
                ) s
                WHERE c.id = s.cuenta_id;
            ");

            migrationBuilder.DropTable(
                name: "saldos_cuenta");
        }
    }
}
