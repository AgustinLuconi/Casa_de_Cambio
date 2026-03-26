using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaCambio.Server.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEmailYTokensAUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "usuarios",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "email_confirmado",
                table: "usuarios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "token_confirmacion",
                table: "usuarios",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "token_expiracion",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token_recuperacion",
                table: "usuarios",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "email_confirmado",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "token_confirmacion",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "token_expiracion",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "token_recuperacion",
                table: "usuarios");
        }
    }
}
