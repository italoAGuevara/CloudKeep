using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TrabajoCopiaFiltrosArchivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CopiaActualizacionDesdeUtc",
                table: "Trabajos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CopiaActualizacionHastaUtc",
                table: "Trabajos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CopiaCreacionDesdeUtc",
                table: "Trabajos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CopiaCreacionHastaUtc",
                table: "Trabajos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CopiaTamanoMaxBytes",
                table: "Trabajos",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CopiaTamanoMinBytes",
                table: "Trabajos",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CopiaActualizacionDesdeUtc",
                table: "Trabajos");

            migrationBuilder.DropColumn(
                name: "CopiaActualizacionHastaUtc",
                table: "Trabajos");

            migrationBuilder.DropColumn(
                name: "CopiaCreacionDesdeUtc",
                table: "Trabajos");

            migrationBuilder.DropColumn(
                name: "CopiaCreacionHastaUtc",
                table: "Trabajos");

            migrationBuilder.DropColumn(
                name: "CopiaTamanoMaxBytes",
                table: "Trabajos");

            migrationBuilder.DropColumn(
                name: "CopiaTamanoMinBytes",
                table: "Trabajos");
        }
    }
}
