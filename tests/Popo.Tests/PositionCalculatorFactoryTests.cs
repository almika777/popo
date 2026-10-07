using NUnit.Framework;
using Popo.Core.Portfolio.Position;

namespace Popo.Tests;

[TestFixture]
public sealed class PositionCalculatorFactoryTests
{
    private readonly PositionCalculatorFactory factory = new();

    [TestCase("RUB", "RUB")]
    [TestCase("SUR", "SUR")]
    [TestCase("RUB", "SUR")]
    [TestCase("SUR", "RUB")]
    public void Create_SelectsFloaterCalculatorForRubleCurrencyPair(string faceUnit, string currencyId)
    {
        Assert.That(factory.Create(faceUnit, currencyId, "Облигация с плавающим купоном"),
            Is.TypeOf<FloaterPositionCalculator>());
    }

    [Test]
    public void Create_PrioritizesZoCalculatorForForeignNominalFloater()
    {
        Assert.That(factory.Create("USD", "RUB", "Флоатер"), Is.TypeOf<ZoPositionCalculator>());
    }

    [TestCase("Флоатер", null)]
    [TestCase(null, "Флоатер")]
    [TestCase("Облигация с плавающим купоном", null)]
    [TestCase(null, "Облигация с плавающим купоном")]
    public void Create_RecognizesFloaterInBondTypeOrSubtype(string? bondType, string? bondSubType)
    {
        Assert.That(factory.Create("RUB", "RUB", bondType, bondSubType),
            Is.TypeOf<FloaterPositionCalculator>());
    }

    [Test]
    public void Create_LeavesUnknownBondTypeOnDefaultCalculator()
    {
        Assert.That(factory.Create("RUB", "RUB", "Облигация с постоянным купоном"),
            Is.TypeOf<DefaultPositionCalculator>());
    }

    [Test]
    public void Create_TwoArgumentCallKeepsDefaultCalculatorBehavior()
    {
        Assert.That(factory.Create("RUB", "RUB"), Is.TypeOf<DefaultPositionCalculator>());
    }
}
