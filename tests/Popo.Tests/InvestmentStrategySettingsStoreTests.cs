using NUnit.Framework;
using Popo.Storage.Entities;
using Popo.Storage.Providers;

namespace Popo.Tests;

[TestFixture]
public sealed class InvestmentStrategySettingsStoreTests
{
    [Test]
    public void ToDomain_PreservesEverySavedPresetSetting()
    {
        var entity = new InvestmentStrategySettingsEntity
        {
            MinimumRating = Popo.Core.Enums.Rating.A_plus,
            MinimumMaturityDays = 21,
            MaximumMaturityDays = 640,
            MinimumMedianDailyVolume = 1_234d,
            OfferWindowDays = 45,
            MaximumYtm = 32d,
            MinimumYtm = 12d,
            InstrumentType = Popo.Core.Recommendations.BondInstrumentType.Floating,
            FaceUnit = "USD",
            CurrencyId = "RUB"
        };

        var settings = InvestmentStrategySettingsStore.ToDomain(entity);

        Assert.Multiple(() =>
        {
            Assert.That(settings.MinimumRating, Is.EqualTo(Popo.Core.Recommendations.CreditRating.A_plus));
            Assert.That(settings.MinimumMaturityDays, Is.EqualTo(21));
            Assert.That(settings.MaximumMaturityDays, Is.EqualTo(640));
            Assert.That(settings.MinimumMedianDailyVolume, Is.EqualTo(1_234d));
            Assert.That(settings.OfferWindowDays, Is.EqualTo(45));
            Assert.That(settings.MaximumYtm, Is.EqualTo(32d));
            Assert.That(settings.MinimumYtm, Is.EqualTo(12d));
            Assert.That(settings.InstrumentType, Is.EqualTo(Popo.Core.Recommendations.BondInstrumentType.Floating));
            Assert.That(settings.FaceUnit, Is.EqualTo("USD"));
            Assert.That(settings.CurrencyId, Is.EqualTo("RUB"));
        });
    }

    [Test]
    public void Apply_PreservesEveryPresetSettingForStorage()
    {
        var entity = new InvestmentStrategySettingsEntity();
        var settings = new Popo.Core.Recommendations.InvestmentStrategySettings(
            Popo.Core.Recommendations.CreditRating.BB_plus,
            21,
            640,
            1_234d,
            45,
            32d,
            12d,
            Popo.Core.Recommendations.BondInstrumentType.Floating,
            "USD",
            "RUB");

        InvestmentStrategySettingsStore.Apply(entity, settings);

        Assert.Multiple(() =>
        {
            Assert.That(entity.MinimumRating, Is.EqualTo(Popo.Core.Enums.Rating.BB_plus));
            Assert.That(entity.MinimumMaturityDays, Is.EqualTo(21));
            Assert.That(entity.MaximumMaturityDays, Is.EqualTo(640));
            Assert.That(entity.MinimumMedianDailyVolume, Is.EqualTo(1_234d));
            Assert.That(entity.OfferWindowDays, Is.EqualTo(45));
            Assert.That(entity.MaximumYtm, Is.EqualTo(32d));
            Assert.That(entity.MinimumYtm, Is.EqualTo(12d));
            Assert.That(entity.InstrumentType, Is.EqualTo(Popo.Core.Recommendations.BondInstrumentType.Floating));
            Assert.That(entity.FaceUnit, Is.EqualTo("USD"));
            Assert.That(entity.CurrencyId, Is.EqualTo("RUB"));
        });
    }
}
