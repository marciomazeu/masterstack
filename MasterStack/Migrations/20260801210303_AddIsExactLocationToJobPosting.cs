using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterStack.Migrations
{
    /// <inheritdoc />
    public partial class AddIsExactLocationToJobPosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsExactLocation",
                table: "JobPostings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsExactLocation",
                table: "JobPostings");
        }
    }
}
