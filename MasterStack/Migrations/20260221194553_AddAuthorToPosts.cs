using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterStack.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorToPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuthorProfileId",
                table: "BlogPosts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_AuthorProfileId",
                table: "BlogPosts",
                column: "AuthorProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_BlogPosts_AuthorProfiles_AuthorProfileId",
                table: "BlogPosts",
                column: "AuthorProfileId",
                principalTable: "AuthorProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlogPosts_AuthorProfiles_AuthorProfileId",
                table: "BlogPosts");

            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_AuthorProfileId",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "AuthorProfileId",
                table: "BlogPosts");
        }
    }
}
