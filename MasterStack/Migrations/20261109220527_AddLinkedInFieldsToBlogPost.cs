using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterStack.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkedInFieldsToBlogPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLinkedInPublished",
                table: "BlogPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInPostText",
                table: "BlogPosts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInPostUrl",
                table: "BlogPosts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublishToLinkedIn",
                table: "BlogPosts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLinkedInPublished",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "LinkedInPostText",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "LinkedInPostUrl",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "PublishToLinkedIn",
                table: "BlogPosts");
        }
    }
}
