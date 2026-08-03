using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterStack.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToJobPosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "SalaryMax",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "SalaryMin",
                table: "JobPostings");

            migrationBuilder.RenameColumn(
                name: "ExternalId",
                table: "JobPostings",
                newName: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "JobPostings",
                newName: "ExternalId");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "JobPostings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMax",
                table: "JobPostings",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMin",
                table: "JobPostings",
                type: "numeric",
                nullable: true);
        }
    }
}
