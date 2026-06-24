using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hikidashi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "facts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    keywords = table.Column<string[]>(type: "text[]", nullable: false),
                    enriched = table.Column<bool>(
                        type: "boolean",
                        nullable: false,
                        defaultValue: false
                    ),
                    metadata = table.Column<string>(
                        type: "jsonb",
                        nullable: false,
                        defaultValueSql: "'{}'::jsonb"
                    ),
                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    updated_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facts", x => x.id);
                }
            );

            migrationBuilder
                .CreateIndex(name: "facts_keywords_gin", table: "facts", column: "keywords")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "facts");
        }
    }
}
