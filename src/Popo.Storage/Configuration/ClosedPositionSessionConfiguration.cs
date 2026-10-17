using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Popo.Storage.Entities;

namespace Popo.Storage.Configuration;

public sealed class ClosedPositionSessionConfiguration : IEntityTypeConfiguration<ClosedPositionSessionEntity>
{
    public void Configure(EntityTypeBuilder<ClosedPositionSessionEntity> entity)
    {
        entity.ToTable("ClosedPositionSessions");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.HasIndex(x => new { x.SecId, x.BoardId, x.CurrencyId, x.StartDate });
        entity.HasIndex(x => x.EndDate).IsDescending();
        entity.Property(x => x.BoughtQuantity).HasPrecision(28, 8);
        entity.Property(x => x.BoughtCleanAmount).HasPrecision(28, 8);
        entity.Property(x => x.SoldCleanAmount).HasPrecision(28, 8);
        entity.Property(x => x.Commission).HasPrecision(28, 8);
        entity.Property(x => x.AmortizationIncome).HasPrecision(28, 8);
        entity.Property(x => x.BodyPnl).HasPrecision(28, 8);
        entity.Property(x => x.CouponIncome).HasPrecision(28, 8);
        entity.Property(x => x.TotalPnl).HasPrecision(28, 8);
        entity.Property(x => x.TotalPnlPercent).HasPrecision(28, 8);
        entity.Property(x => x.AnnualizedPnlPercent).HasPrecision(28, 8);
    }
}
