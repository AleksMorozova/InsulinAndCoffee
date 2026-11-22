using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InsulinAndCoffee.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUsageMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "AssistantConversations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiModelPrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InputPricePerMillionTokens = table.Column<decimal>(type: "numeric(28,14)", precision: 28, scale: 14, nullable: false),
                    CachedInputPricePerMillionTokens = table.Column<decimal>(type: "numeric(28,14)", precision: 28, scale: 14, nullable: true),
                    OutputPricePerMillionTokens = table.Column<decimal>(type: "numeric(28,14)", precision: 28, scale: 14, nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModelPrices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Feature = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderRequestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProviderCalled = table.Column<bool>(type: "boolean", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    CachedInputTokens = table.Column<int>(type: "integer", nullable: true),
                    ReasoningTokens = table.Column<int>(type: "integer", nullable: true),
                    TotalTokens = table.Column<int>(type: "integer", nullable: true),
                    InputCostUsd = table.Column<decimal>(type: "numeric(28,14)", precision: 28, scale: 14, nullable: true),
                    CachedInputCostUsd = table.Column<decimal>(type: "numeric(28,14)", precision: 28, scale: 14, nullable: true),
                    OutputCostUsd = table.Column<decimal>(type: "numeric(28,14)", precision: 28, scale: 14, nullable: true),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(28,14)", precision: 28, scale: 14, nullable: true),
                    PricingVersion = table.Column<Guid>(type: "uuid", nullable: true),
                    InputPricePerMillionTokens = table.Column<decimal>(type: "numeric(28,14)", precision: 28, scale: 14, nullable: true),
                    CachedInputPricePerMillionTokens = table.Column<decimal>(type: "numeric(28,14)", precision: 28, scale: 14, nullable: true),
                    OutputPricePerMillionTokens = table.Column<decimal>(type: "numeric(28,14)", precision: 28, scale: 14, nullable: true),
                    CacheStatus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiModelPrices_Provider_Model_EffectiveFromUtc",
                table: "AiModelPrices",
                columns: new[] { "Provider", "Model", "EffectiveFromUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_Model_Feature_StartedAtUtc",
                table: "AiUsageRecords",
                columns: new[] { "Model", "Feature", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_OperationId",
                table: "AiUsageRecords",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_StartedAtUtc",
                table: "AiUsageRecords",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_UserId_StartedAtUtc",
                table: "AiUsageRecords",
                columns: new[] { "UserId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiModelPrices");

            migrationBuilder.DropTable(
                name: "AiUsageRecords");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "AssistantConversations");
        }
    }
}
