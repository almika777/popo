using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Popo.Storage.Migrations;

[DbContext(typeof(PopoDbContext))]
[Migration("20260806120000_AddCurrencyRates")]
public partial class AddCurrencyRates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CurrencyRates",
            columns: table => new
            {
                RateDate = table.Column<DateOnly>(type: "date", nullable: false),
                CurrencyCode = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Nominal = table.Column<double>(type: "double precision", nullable: false),
                Value = table.Column<double>(type: "double precision", nullable: false),
                UnitRate = table.Column<double>(type: "double precision", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CurrencyRates", x => new { x.RateDate, x.CurrencyCode });
            });

        migrationBuilder.CreateIndex(
            name: "IX_CurrencyRates_CurrencyCode",
            table: "CurrencyRates",
            column: "CurrencyCode");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CurrencyRates");
    }
}
