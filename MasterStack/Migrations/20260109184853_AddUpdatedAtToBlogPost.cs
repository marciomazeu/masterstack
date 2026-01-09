using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterStack.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedAtToBlogPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Culture",
                keyValue: "es-ES");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "BlogPosts",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.InsertData(
                table: "Languages",
                columns: new[] { "Culture", "FlagClass", "IsActive", "Name" },
                values: new object[] { "fr-FR", "fi-ca", false, "Français" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Culture",
                keyValue: "fr-FR");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "BlogPosts");

            migrationBuilder.InsertData(
                table: "Languages",
                columns: new[] { "Culture", "FlagClass", "IsActive", "Name" },
                values: new object[] { "es-ES", "fi-es", false, "Español" });
        }
    }
}
