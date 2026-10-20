using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class ClosedPositionSessionFieldsUseDouble : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "TotalPnlPercent",
                table: "ClosedPositionSessions",
                type: "double precision",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,8)",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "TotalPnl",
                table: "ClosedPositionSessions",
                type: "double precision",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,8)",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "SoldCleanAmount",
                table: "ClosedPositionSessions",
                type: "double precision",
                precision: 28,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,8)",
                oldPrecision: 28,
                oldScale: 8);

            migrationBuilder.AlterColumn<double>(
                name: "CouponIncome",
                table: "ClosedPositionSessions",
                type: "double precision",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,8)",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "Commission",
                table: "ClosedPositionSessions",
                type: "double precision",
                precision: 28,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,8)",
                oldPrecision: 28,
                oldScale: 8);

            migrationBuilder.AlterColumn<double>(
                name: "BoughtQuantity",
                table: "ClosedPositionSessions",
                type: "double precision",
                precision: 28,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,8)",
                oldPrecision: 28,
                oldScale: 8);

            migrationBuilder.AlterColumn<double>(
                name: "BoughtCleanAmount",
                table: "ClosedPositionSessions",
                type: "double precision",
                precision: 28,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,8)",
                oldPrecision: 28,
                oldScale: 8);

            migrationBuilder.AlterColumn<double>(
                name: "BodyPnl",
                table: "ClosedPositionSessions",
                type: "double precision",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,8)",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "AnnualizedPnlPercent",
                table: "ClosedPositionSessions",
                type: "double precision",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,8)",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "AmortizationIncome",
                table: "ClosedPositionSessions",
                type: "double precision",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,8)",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPnlPercent",
                table: "ClosedPositionSessions",
                type: "numeric(28,8)",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalPnl",
                table: "ClosedPositionSessions",
                type: "numeric(28,8)",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "SoldCleanAmount",
                table: "ClosedPositionSessions",
                type: "numeric(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 28,
                oldScale: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "CouponIncome",
                table: "ClosedPositionSessions",
                type: "numeric(28,8)",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Commission",
                table: "ClosedPositionSessions",
                type: "numeric(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 28,
                oldScale: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "BoughtQuantity",
                table: "ClosedPositionSessions",
                type: "numeric(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 28,
                oldScale: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "BoughtCleanAmount",
                table: "ClosedPositionSessions",
                type: "numeric(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 28,
                oldScale: 8);

            migrationBuilder.AlterColumn<decimal>(
                name: "BodyPnl",
                table: "ClosedPositionSessions",
                type: "numeric(28,8)",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AnnualizedPnlPercent",
                table: "ClosedPositionSessions",
                type: "numeric(28,8)",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AmortizationIncome",
                table: "ClosedPositionSessions",
                type: "numeric(28,8)",
                precision: 28,
                scale: 8,
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldPrecision: 28,
                oldScale: 8,
                oldNullable: true);
        }
    }
}
