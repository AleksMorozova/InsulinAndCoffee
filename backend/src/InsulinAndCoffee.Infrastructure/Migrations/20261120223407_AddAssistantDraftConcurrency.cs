using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsulinAndCoffee.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantDraftConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DraftJson",
                table: "AssistantConversations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DraftVersion",
                table: "AssistantConversations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "AssistantRequestReceipts",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantRequestReceipts", x => new { x.UserId, x.RequestId });
                    table.ForeignKey(
                        name: "FK_AssistantRequestReceipts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "AssistantConversations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantRequestReceipts_ConversationId",
                table: "AssistantRequestReceipts",
                column: "ConversationId");

            // Preserve the latest existing draft independently of transcript retention.
            migrationBuilder.Sql("""
                UPDATE "AssistantConversations" AS conversation
                SET "DraftJson" = latest.draft::text
                FROM (
                    SELECT DISTINCT ON ("ConversationId") "ConversationId",
                        COALESCE(NULLIF("ResponseJson"::jsonb -> 'Draft', 'null'::jsonb),
                                 NULLIF("ResponseJson"::jsonb -> 'draft', 'null'::jsonb)) AS draft
                    FROM "AssistantMessages"
                    WHERE "ResponseJson" IS NOT NULL
                      AND jsonb_typeof(COALESCE(NULLIF("ResponseJson"::jsonb -> 'Draft', 'null'::jsonb),
                                               NULLIF("ResponseJson"::jsonb -> 'draft', 'null'::jsonb))) = 'object'
                    ORDER BY "ConversationId", "CreatedAt" DESC, "Id" DESC
                ) AS latest
                WHERE conversation."Id" = latest."ConversationId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantRequestReceipts");

            migrationBuilder.DropColumn(
                name: "DraftJson",
                table: "AssistantConversations");

            migrationBuilder.DropColumn(
                name: "DraftVersion",
                table: "AssistantConversations");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "AssistantConversations");
        }
    }
}
