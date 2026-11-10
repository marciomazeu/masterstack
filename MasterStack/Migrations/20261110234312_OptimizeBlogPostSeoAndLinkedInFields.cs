using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterStack.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeBlogPostSeoAndLinkedInFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlogPostTranslations_Culture",
                table: "BlogPostTranslations");

            migrationBuilder.AddColumn<string>(
                name: "ImageAltText",
                table: "BlogPostTranslations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "BlogPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_BlogPostTranslations_BlogPostId",
                table: "BlogPostTranslations",
                column: "BlogPostId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPostTranslations_Culture_Slug",
                table: "BlogPostTranslations",
                columns: new[] { "Culture", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlogPostTranslations_BlogPostId",
                table: "BlogPostTranslations");

            migrationBuilder.DropIndex(
                name: "IX_BlogPostTranslations_Culture_Slug",
                table: "BlogPostTranslations");

            migrationBuilder.DropColumn(
                name: "ImageAltText",
                table: "BlogPostTranslations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "BlogPosts");

            migrationBuilder.CreateIndex(
                name: "IX_BlogPostTranslations_Culture",
                table: "BlogPostTranslations",
                column: "Culture");
        }
    }
}
