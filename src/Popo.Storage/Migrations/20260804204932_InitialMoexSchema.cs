using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Popo.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialMoexSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveSecEntities",
                columns: table => new
                {
                    SecId = table.Column<string>(type: "text", nullable: false),
                    ShortName = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Isin = table.Column<string>(type: "text", nullable: false),
                    EmitentId = table.Column<int>(type: "integer", nullable: true),
                    EmitentTitle = table.Column<string>(type: "text", nullable: true),
                    PrimaryBoardId = table.Column<string>(type: "text", nullable: false),
                    MarketPriceBoardId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveSecEntities", x => x.SecId);
                });

            migrationBuilder.CreateTable(
                name: "BondRatings",
                columns: table => new
                {
                    Isin = table.Column<string>(type: "text", nullable: false),
                    Acra = table.Column<int>(type: "integer", nullable: true),
                    Expert = table.Column<int>(type: "integer", nullable: true),
                    NRA = table.Column<int>(type: "integer", nullable: true),
                    NKR = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BondRatings", x => x.Isin);
                });

            migrationBuilder.CreateTable(
                name: "MoexAmortsEntities",
                columns: table => new
                {
                    SecId = table.Column<string>(type: "text", nullable: false),
                    AmortDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Isin = table.Column<string>(type: "text", nullable: true),
                    InitialFaceValue = table.Column<double>(type: "double precision", nullable: false),
                    FaceValue = table.Column<double>(type: "double precision", nullable: false),
                    FaceUnit = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: true),
                    ValuePercent = table.Column<double>(type: "double precision", nullable: true),
                    ValueInRub = table.Column<double>(type: "double precision", nullable: true),
                    BoardId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoexAmortsEntities", x => new { x.SecId, x.AmortDate });
                });

            migrationBuilder.CreateTable(
                name: "MoexBonds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SecId = table.Column<string>(type: "text", nullable: false),
                    IssueName = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ShortName = table.Column<string>(type: "text", nullable: false),
                    RegNumber = table.Column<string>(type: "text", nullable: false),
                    Isin = table.Column<string>(type: "text", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MatDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OfferDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InitialFaceValue = table.Column<double>(type: "double precision", nullable: true),
                    FaceValue = table.Column<double>(type: "double precision", nullable: false),
                    FaceUnit = table.Column<string>(type: "text", nullable: false),
                    LatName = table.Column<string>(type: "text", nullable: false),
                    StartDateMoex = table.Column<DateOnly>(type: "date", nullable: true),
                    HasProspectus = table.Column<bool>(type: "boolean", nullable: true),
                    DecisionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsConcessionAgreement = table.Column<bool>(type: "boolean", nullable: true),
                    HasDefault = table.Column<bool>(type: "boolean", nullable: true),
                    HasTechnicalDefault = table.Column<bool>(type: "boolean", nullable: true),
                    ProgramRegistryNumber = table.Column<string>(type: "text", nullable: false),
                    EmitentMismatchCurrent = table.Column<int>(type: "integer", nullable: true),
                    ListLevel = table.Column<int>(type: "integer", nullable: true),
                    DaysToRedemption = table.Column<int>(type: "integer", nullable: true),
                    IssueSize = table.Column<long>(type: "bigint", nullable: false),
                    IsQualifiedInvestors = table.Column<bool>(type: "boolean", nullable: true),
                    CouponFrequency = table.Column<int>(type: "integer", nullable: true),
                    NextCouponDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CouponPercent = table.Column<double>(type: "double precision", nullable: true),
                    CouponValue = table.Column<double>(type: "double precision", nullable: false),
                    MorningSession = table.Column<bool>(type: "boolean", nullable: true),
                    EveningSession = table.Column<bool>(type: "boolean", nullable: true),
                    WeekendSession = table.Column<bool>(type: "boolean", nullable: true),
                    RegistryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    BondType = table.Column<string>(type: "text", nullable: false),
                    BondSubType = table.Column<string>(type: "text", nullable: false),
                    TypeName = table.Column<string>(type: "text", nullable: false),
                    Group = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    GroupName = table.Column<string>(type: "text", nullable: false),
                    EmitterId = table.Column<int>(type: "integer", nullable: true),
                    Updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoexBonds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MoexBondSecurities",
                columns: table => new
                {
                    SecId = table.Column<string>(type: "text", nullable: false),
                    BoardId = table.Column<string>(type: "text", nullable: false),
                    ShortName = table.Column<string>(type: "text", nullable: false),
                    PrevWaPrice = table.Column<double>(type: "double precision", nullable: true),
                    YieldAtPrevWaPrice = table.Column<double>(type: "double precision", nullable: true),
                    CouponValue = table.Column<double>(type: "double precision", nullable: false),
                    NextCouponDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AccruedInt = table.Column<double>(type: "double precision", nullable: true),
                    PrevPrice = table.Column<double>(type: "double precision", nullable: true),
                    LotSize = table.Column<long>(type: "bigint", nullable: false),
                    FaceValue = table.Column<double>(type: "double precision", nullable: false),
                    BoardName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MatDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Decimals = table.Column<int>(type: "integer", nullable: false),
                    CouponPeriod = table.Column<int>(type: "integer", nullable: false),
                    IssueSize = table.Column<long>(type: "bigint", nullable: true),
                    PrevLegalClosePrice = table.Column<double>(type: "double precision", nullable: true),
                    PrevTradeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SecName = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    MarketCode = table.Column<string>(type: "text", nullable: true),
                    InstrId = table.Column<string>(type: "text", nullable: true),
                    SectorId = table.Column<string>(type: "text", nullable: true),
                    MinStep = table.Column<double>(type: "double precision", nullable: true),
                    FaceUnit = table.Column<string>(type: "text", nullable: false),
                    BuybackPrice = table.Column<double>(type: "double precision", nullable: true),
                    BuybackDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Isin = table.Column<string>(type: "text", nullable: true),
                    LatName = table.Column<string>(type: "text", nullable: true),
                    RegNumber = table.Column<string>(type: "text", nullable: true),
                    CurrencyId = table.Column<string>(type: "text", nullable: true),
                    IssueSizePlaced = table.Column<long>(type: "bigint", nullable: true),
                    ListLevel = table.Column<int>(type: "integer", nullable: true),
                    SecType = table.Column<string>(type: "text", nullable: true),
                    CouponPercent = table.Column<double>(type: "double precision", nullable: true),
                    OfferDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SettleDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LotValue = table.Column<double>(type: "double precision", nullable: true),
                    FaceValueOnSettleDate = table.Column<double>(type: "double precision", nullable: true),
                    CallOptionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PutOptionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DateYieldFromIssuer = table.Column<DateOnly>(type: "date", nullable: true),
                    BondType = table.Column<string>(type: "text", nullable: true),
                    BondSubType = table.Column<string>(type: "text", nullable: true),
                    Updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoexBondSecurities", x => new { x.SecId, x.BoardId });
                });

            migrationBuilder.CreateTable(
                name: "MoexCouponsEntities",
                columns: table => new
                {
                    SecId = table.Column<string>(type: "text", nullable: false),
                    CouponDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Isin = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InitialFaceValue = table.Column<double>(type: "double precision", nullable: false),
                    FaceValue = table.Column<double>(type: "double precision", nullable: false),
                    FaceUnit = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: true),
                    ValuePercent = table.Column<double>(type: "double precision", nullable: true),
                    ValueInRub = table.Column<double>(type: "double precision", nullable: true),
                    BoardId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoexCouponsEntities", x => new { x.SecId, x.CouponDate });
                });

            migrationBuilder.CreateTable(
                name: "MoexEmitents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Inn = table.Column<string>(type: "text", nullable: true),
                    Okpo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoexEmitents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MoexHistoryYieldsEntities",
                columns: table => new
                {
                    SecId = table.Column<string>(type: "text", nullable: false),
                    BoardId = table.Column<string>(type: "text", nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NumTrades = table.Column<double>(type: "double precision", nullable: true),
                    Value = table.Column<double>(type: "double precision", nullable: true),
                    Low = table.Column<double>(type: "double precision", nullable: true),
                    High = table.Column<double>(type: "double precision", nullable: true),
                    Close = table.Column<double>(type: "double precision", nullable: true),
                    LegalClosePrice = table.Column<double>(type: "double precision", nullable: true),
                    AccInt = table.Column<double>(type: "double precision", nullable: true),
                    WapPrice = table.Column<double>(type: "double precision", nullable: true),
                    YieldClose = table.Column<double>(type: "double precision", nullable: true),
                    Open = table.Column<double>(type: "double precision", nullable: true),
                    Volume = table.Column<double>(type: "double precision", nullable: true),
                    MarketPrice2 = table.Column<double>(type: "double precision", nullable: true),
                    MarketPrice3 = table.Column<double>(type: "double precision", nullable: true),
                    Mp2ValTrd = table.Column<double>(type: "double precision", nullable: true),
                    MarketPrice3TradesValue = table.Column<double>(type: "double precision", nullable: true),
                    MatDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Duration = table.Column<double>(type: "double precision", nullable: true),
                    YieldAtWap = table.Column<double>(type: "double precision", nullable: true),
                    IricpiClose = table.Column<double>(type: "double precision", nullable: true),
                    BeiClose = table.Column<double>(type: "double precision", nullable: true),
                    CouponPercent = table.Column<double>(type: "double precision", nullable: false),
                    CouponValue = table.Column<double>(type: "double precision", nullable: false),
                    BuybackDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastTradeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FaceValue = table.Column<double>(type: "double precision", nullable: false),
                    CurrencyId = table.Column<string>(type: "text", nullable: true),
                    CbrClose = table.Column<double>(type: "double precision", nullable: true),
                    YieldToOffer = table.Column<double>(type: "double precision", nullable: true),
                    YieldLastCoupon = table.Column<double>(type: "double precision", nullable: true),
                    OfferDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FaceUnit = table.Column<string>(type: "text", nullable: true),
                    TradingSession = table.Column<int>(type: "integer", nullable: true),
                    CallOptionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CallOptionYield = table.Column<double>(type: "double precision", nullable: true),
                    CallOptionDuration = table.Column<double>(type: "double precision", nullable: true),
                    PutOptionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DateYieldFromIssuer = table.Column<DateOnly>(type: "date", nullable: true),
                    TradeSessionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ZSpread = table.Column<double>(type: "double precision", nullable: true),
                    ZSpreadAtWapPrice = table.Column<double>(type: "double precision", nullable: true),
                    BondType = table.Column<string>(type: "text", nullable: true),
                    BondSubType = table.Column<string>(type: "text", nullable: true),
                    ShortName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoexHistoryYieldsEntities", x => new { x.TradeDate, x.SecId, x.BoardId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_MoexAmortsEntities_AmortDate",
                table: "MoexAmortsEntities",
                column: "AmortDate",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_MoexBonds_Isin",
                table: "MoexBonds",
                column: "Isin");

            migrationBuilder.CreateIndex(
                name: "IX_MoexBonds_SecId",
                table: "MoexBonds",
                column: "SecId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MoexCouponsEntities_CouponDate",
                table: "MoexCouponsEntities",
                column: "CouponDate",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_MoexHistoryYieldsEntities_SecId_BoardId_TradeDate",
                table: "MoexHistoryYieldsEntities",
                columns: new[] { "SecId", "BoardId", "TradeDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveSecEntities");

            migrationBuilder.DropTable(
                name: "BondRatings");

            migrationBuilder.DropTable(
                name: "MoexAmortsEntities");

            migrationBuilder.DropTable(
                name: "MoexBonds");

            migrationBuilder.DropTable(
                name: "MoexBondSecurities");

            migrationBuilder.DropTable(
                name: "MoexCouponsEntities");

            migrationBuilder.DropTable(
                name: "MoexEmitents");

            migrationBuilder.DropTable(
                name: "MoexHistoryYieldsEntities");
        }
    }
}
