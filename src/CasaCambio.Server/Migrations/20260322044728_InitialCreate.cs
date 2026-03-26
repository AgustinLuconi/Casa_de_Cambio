using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaCambio.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Baseline: schema ya existe via EnsureCreated. Este marcador registra el punto de partida.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
