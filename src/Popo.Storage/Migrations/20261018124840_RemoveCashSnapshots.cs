using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCashSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashSnapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<double>(type: "double precision", precision: 20, scale: 6, nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    CurrencyId = table.Column<string>(type: "text", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashSnapshots_CurrencyId_SnapshotDate",
                table: "CashSnapshots",
                columns: new[] { "CurrencyId", "SnapshotDate" },
                unique: true);
        }
    }
}
