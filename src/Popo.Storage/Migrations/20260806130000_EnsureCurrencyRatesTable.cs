using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Popo.Storage.Migrations;

[DbContext(typeof(PopoDbContext))]
[Migration("20260806130000_EnsureCurrencyRatesTable")]
public partial class EnsureCurrencyRatesTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "CurrencyRates" (
                "RateDate" date NOT NULL,
                "CurrencyCode" text NOT NULL,
                "Name" text NOT NULL,
                "Nominal" double precision NOT NULL,
                "Value" double precision NOT NULL,
                "UnitRate" double precision NOT NULL,
                CONSTRAINT "PK_CurrencyRates" PRIMARY KEY ("RateDate", "CurrencyCode")
            );
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_CurrencyRates_CurrencyCode"
            ON "CurrencyRates" ("CurrencyCode");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
