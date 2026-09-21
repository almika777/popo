using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations;

public partial class AddMoneyMarketFunds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MoneyMarketFunds",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SecId = table.Column<string>(type: "text", nullable: false),
                BoardId = table.Column<string>(type: "text", nullable: false),
                Quantity = table.Column<double>(type: "double precision", nullable: false),
                AveragePrice = table.Column<double>(type: "double precision", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_MoneyMarketFunds", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_MoneyMarketFunds_SecId_BoardId",
            table: "MoneyMarketFunds",
            columns: new[] { "SecId", "BoardId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "MoneyMarketFunds");
}
