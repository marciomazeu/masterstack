using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterStack.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeletedToBlogPostTranslation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "BlogPostTranslations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "BlogPostTranslations");
        }
    }
}
