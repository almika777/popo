using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokerTradeSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalTradeId",
                table: "Trades",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Trades",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trades_Source_ExternalTradeId",
                table: "Trades",
                columns: new[] { "Source", "ExternalTradeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trades_Source_ExternalTradeId",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "ExternalTradeId",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Trades");
        }
    }
}
