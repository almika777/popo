using Microsoft.EntityFrameworkCore;
using Popo.Core.Enums;
using Popo.Core.Recommendations;
using Popo.Storage.Entities;
using Popo.Storage.Entities.Moex;

namespace Popo.Storage;

public sealed class PopoDbContext(DbContextOptions<PopoDbContext> options) : DbContext(options)
{
    public DbSet<DailyVolumeStatisticsEntity> DailyVolumeStatistics => Set<DailyVolumeStatisticsEntity>();
    public DbSet<SecInfoEntity> ActiveSecEntities => Set<SecInfoEntity>();
    public DbSet<MoexBondEntity> MoexBonds => Set<MoexBondEntity>();
    public DbSet<MoexBondSecurityEntity> MoexBondSecurities => Set<MoexBondSecurityEntity>();
    public DbSet<MoexHistoryYieldsEntity> MoexHistoryYieldsEntities => Set<MoexHistoryYieldsEntity>();
    public DbSet<MoexCouponsEntity> MoexCouponsEntities => Set<MoexCouponsEntity>();
    public DbSet<MoexAmortsEntity> MoexAmortsEntities => Set<MoexAmortsEntity>();
    public DbSet<MoexEmitentEntity> MoexEmitents => Set<MoexEmitentEntity>();
    public DbSet<BondRatingEntity> BondRatings => Set<BondRatingEntity>();
    public DbSet<CurrencyRateEntity> CurrencyRates => Set<CurrencyRateEntity>();
    public DbSet<PortfolioValuationEntity> PortfolioValuations => Set<PortfolioValuationEntity>();
    public DbSet<PortfolioCashFlowEntity> PortfolioCashFlows => Set<PortfolioCashFlowEntity>();
    public DbSet<PortfolioTradeEntity> PortfolioTrades => Set<PortfolioTradeEntity>();
    public DbSet<CashSnapshotEntity> CashSnapshots => Set<CashSnapshotEntity>();
    public DbSet<MoneyMarketFundEntity> MoneyMarketFunds => Set<MoneyMarketFundEntity>();
    public DbSet<MoneyMarketFundOperationEntity> MoneyMarketFundOperations => Set<MoneyMarketFundOperationEntity>();
    public DbSet<InvestmentStrategySettingsEntity> InvestmentStrategySettings => Set<InvestmentStrategySettingsEntity>();
    public DbSet<PositionRecommendationStateEntity> PositionRecommendationStates => Set<PositionRecommendationStateEntity>();
    public DbSet<InitializationJobStateEntity> InitializationJobStates => Set<InitializationJobStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyVolumeStatisticsEntity>().HasKey(x => x.SecId);
        modelBuilder.Entity<SecInfoEntity>().HasKey(x => x.SecId);
        modelBuilder.Entity<MoexBondEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Isin);
            entity.HasIndex(x => x.SecId).IsUnique();
        });
        modelBuilder.Entity<MoexBondSecurityEntity>().HasKey(x => new { x.SecId, x.BoardId });
        modelBuilder.Entity<MoexHistoryYieldsEntity>(entity =>
        {
            entity.HasKey(x => new { x.TradeDate, x.SecId, x.BoardId });
            entity.HasIndex(x => new { x.SecId, x.BoardId, x.TradeDate });
        });
        modelBuilder.Entity<MoexCouponsEntity>(entity =>
        {
            entity.HasKey(x => new { x.SecId, x.CouponDate });
            entity.HasIndex(x => x.CouponDate).IsDescending();
        });
        modelBuilder.Entity<MoexAmortsEntity>(entity =>
        {
            entity.HasKey(x => new { x.SecId, x.AmortDate });
            entity.HasIndex(x => x.AmortDate).IsDescending();
        });
        modelBuilder.Entity<MoexEmitentEntity>().HasKey(x => x.Id);
        modelBuilder.Entity<BondRatingEntity>().HasKey(x => x.Isin);
        modelBuilder.Entity<CurrencyRateEntity>(entity =>
        {
            entity.HasKey(x => new { x.RateDate, x.CurrencyCode });
            entity.HasIndex(x => x.CurrencyCode);
        });
        modelBuilder.Entity<PortfolioValuationEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Date).IsUnique();
            entity.Property(x => x.TotalValue).HasPrecision(20, 6);
        });
        modelBuilder.Entity<PortfolioCashFlowEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Date);
            entity.Property(x => x.Amount).HasPrecision(20, 6);
        });
        modelBuilder.Entity<PortfolioTradeEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TradeDate, x.SecId, x.BoardId });
            entity.Property(x => x.Quantity).HasPrecision(20, 6);
            entity.Property(x => x.Price).HasPrecision(20, 6);
            entity.Property(x => x.FaceValue).HasPrecision(20, 6);
            entity.Property(x => x.AccruedInterest).HasPrecision(20, 6);
            entity.Property(x => x.Commission).HasPrecision(20, 6);
        });
        modelBuilder.Entity<CashSnapshotEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CurrencyId, x.SnapshotDate }).IsUnique();
            entity.Property(x => x.Amount).HasPrecision(20, 6);
        });
        modelBuilder.Entity<MoneyMarketFundEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SecId, x.BoardId }).IsUnique();
        });
        modelBuilder.Entity<MoneyMarketFundOperationEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Date, x.SecId });
            entity.Property(x => x.Quantity).HasPrecision(20, 6);
            entity.Property(x => x.Price).HasPrecision(20, 6);
            entity.Property(x => x.Commission).HasPrecision(20, 6);
        });
        modelBuilder.Entity<InvestmentStrategySettingsEntity>(entity =>
        {
            var defaults = Popo.Core.Recommendations.InvestmentStrategySettings.Defaults;
            entity.HasKey(x => x.Id);
            entity.HasData(new InvestmentStrategySettingsEntity
            {
                Id = 1,
                Name = "Основной",
                IsActive = true,
                MinimumRating = (Rating)defaults.MinimumRating,
                MinimumMaturityDays = defaults.MinimumMaturityDays,
                MaximumMaturityDays = defaults.MaximumMaturityDays,
                MinimumMedianDailyVolume = defaults.MinimumMedianDailyVolume,
                OfferWindowDays = defaults.OfferWindowDays,
                MaximumYtm = defaults.MaximumYtm,
                MinimumYtm = defaults.MinimumYtm,
                InstrumentType = defaults.InstrumentType,
                FaceUnit = defaults.FaceUnit,
                CurrencyId = defaults.CurrencyId,
                UpdatedAt = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero)
            });
        });
        modelBuilder.Entity<PositionRecommendationStateEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.ResultJson).HasColumnType("jsonb");
            entity.Property(x => x.Version).IsConcurrencyToken().ValueGeneratedNever();
        });
        modelBuilder.Entity<InitializationJobStateEntity>(entity =>
        {
            entity.HasKey(x => new { x.BootstrapVersion, x.JobKey });
            entity.Property(x => x.BootstrapVersion).HasMaxLength(32);
            entity.Property(x => x.JobKey).HasMaxLength(128);
            entity.Property(x => x.Phase).HasMaxLength(256);
            entity.Property(x => x.LastError).HasMaxLength(4000);
        });
        base.OnModelCreating(modelBuilder);
    }
}
