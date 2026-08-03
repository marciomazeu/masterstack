using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterStack.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLocationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bio_EN",
                table: "AspNetUsers");

            migrationBuilder.RenameColumn(
                name: "ZipCode",
                table: "AspNetUsers",
                newName: "StateOrRegion");

            migrationBuilder.RenameColumn(
                name: "State",
                table: "AspNetUsers",
                newName: "PostalCode");

            migrationBuilder.RenameColumn(
                name: "Bio_FR",
                table: "AspNetUsers",
                newName: "CountryCode");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "AspNetUsers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StateOrRegion",
                table: "AspNetUsers",
                newName: "ZipCode");

            migrationBuilder.RenameColumn(
                name: "PostalCode",
                table: "AspNetUsers",
                newName: "State");

            migrationBuilder.RenameColumn(
                name: "CountryCode",
                table: "AspNetUsers",
                newName: "Bio_FR");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bio_EN",
                table: "AspNetUsers",
                type: "text",
                nullable: true);
        }
    }
}
