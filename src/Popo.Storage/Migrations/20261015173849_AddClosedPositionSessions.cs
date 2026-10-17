using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddClosedPositionSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClosedPositionSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SecId = table.Column<string>(type: "text", nullable: false),
                    BoardId = table.Column<string>(type: "text", nullable: false),
                    CurrencyId = table.Column<string>(type: "text", nullable: false),
                    ShortName = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    HoldingDays = table.Column<int>(type: "integer", nullable: false),
                    BoughtQuantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    BoughtCleanAmount = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    SoldCleanAmount = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    Commission = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    AmortizationIncome = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: true),
                    BodyPnl = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: true),
                    CouponIncome = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: true),
                    TotalPnl = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: true),
                    TotalPnlPercent = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: true),
                    AnnualizedPnlPercent = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: true),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                    IncompleteReason = table.Column<string>(type: "text", nullable: true),
                    CalculatorKind = table.Column<string>(type: "text", nullable: false),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosedPositionSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClosedPositionSessions_EndDate",
                table: "ClosedPositionSessions",
                column: "EndDate",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ClosedPositionSessions_SecId_BoardId_CurrencyId_StartDate",
                table: "ClosedPositionSessions",
                columns: new[] { "SecId", "BoardId", "CurrencyId", "StartDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClosedPositionSessions");
        }
    }
}
