using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MasterStack.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalJobSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalJobSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    EndpointOrSlug = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LocationFilter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TechKeywordsFilter = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    JsonRootArrayPath = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MapTitleField = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MapCompanyField = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MapLocationField = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MapDescriptionField = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MapUrlField = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastImportedCount = table.Column<int>(type: "integer", nullable: false),
                    LastErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalJobSources", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalJobSources");
        }
    }
}
