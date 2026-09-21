using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddCashInvestmentRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashInvestmentRecommendationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CalculationStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSuccessfulAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SnapshotTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashInvestmentRecommendationStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvestmentStrategySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MinimumRating = table.Column<int>(type: "integer", nullable: false),
                    MinimumMaturityDays = table.Column<int>(type: "integer", nullable: false),
                    MaximumMaturityDays = table.Column<int>(type: "integer", nullable: false),
                    CommissionRate = table.Column<double>(type: "double precision", nullable: false),
                    MaxIssuerShare = table.Column<double>(type: "double precision", nullable: false),
                    MaxMedianTurnoverShare = table.Column<double>(type: "double precision", nullable: false),
                    OfferWindowDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentStrategySettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "InvestmentStrategySettings",
                columns: new[] { "Id", "CommissionRate", "MaxIssuerShare", "MaxMedianTurnoverShare", "MaximumMaturityDays", "MinimumMaturityDays", "MinimumRating", "OfferWindowDays", "UpdatedAt" },
                values: new object[] { 1, 0.00040000000000000002, 0.10000000000000001, 0.10000000000000001, 730, 30, 16, 90, new DateTimeOffset(new DateTime(2026, 8, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashInvestmentRecommendationStates");

            migrationBuilder.DropTable(
                name: "InvestmentStrategySettings");
        }
    }
}
