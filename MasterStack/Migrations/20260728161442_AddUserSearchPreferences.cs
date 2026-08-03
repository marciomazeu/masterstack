using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterStack.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSearchPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredJobTitle",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SearchRadiusKm",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredJobTitle",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SearchRadiusKm",
                table: "AspNetUsers");
        }
    }
}
