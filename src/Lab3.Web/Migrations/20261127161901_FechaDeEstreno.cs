using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lab3.Web.Migrations
{
    /// <inheritdoc />
    public partial class FechaDeEstreno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ReleaseDate",
                table: "Titles",
                type: "date",
                nullable: true);

            // Los títulos ya guardados no tienen fecha: se fuerza a que se vuelvan a sincronizar con TMDB.
            migrationBuilder.Sql("UPDATE \"Titles\" SET \"LastSyncedAt\" = '1970-01-01T00:00:00Z';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "Titles");
        }
    }
}
