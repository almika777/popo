using NUnit.Framework;
using Popo.Core.Bonds;
using Popo.Core.Portfolio.Position;

namespace Popo.Tests;

[TestFixture]
public sealed class PositionCalculatorFactoryTests
{
    private readonly PositionCalculatorFactory factory = new();

    [TestCase("Флоатер")]
    [TestCase("флоатер")]
    [TestCase("Облигация с плавающим купоном")]
    [TestCase("облигация с плавающим купоном")]
    public void Create_SelectsFloaterCalculatorForBondType(string bondType)
    {
        Assert.That(factory.Create(bondType),
            Is.TypeOf<FloaterPositionCalculator>());
    }

    [Test]
    public void Create_SelectsFloaterCalculatorForFloater()
    {
        Assert.That(factory.Create("Флоатер"), Is.TypeOf<FloaterPositionCalculator>());
    }

    [Test]
    public void Create_SelectsDefaultForKnownFixedType()
    {
        Assert.That(factory.Create("Облигация с фиксированным (известным) купоном"),
            Is.TypeOf<DefaultPositionCalculator>());
    }

    [TestCase("Флоатер", null)]
    [TestCase(null, "Флоатер")]
    [TestCase("Облигация с плавающим купоном", null)]
    [TestCase(null, "Облигация с плавающим купоном")]
    public void Classifier_RecognizesFloaterInBondTypeOrSubtype(string? bondType, string? bondSubType)
    {
        Assert.That(BondCouponClassifier.IsFloater(bondType, bondSubType), Is.True);
        Assert.That(factory.Create(bondType ?? bondSubType), Is.TypeOf<FloaterPositionCalculator>());
    }

    [Test]
    public void Create_LeavesUnknownBondTypeOnDefaultCalculator()
    {
        Assert.That(factory.Create("Облигация с постоянным купоном"),
            Is.TypeOf<DefaultPositionCalculator>());
    }

    [Test]
    public void Create_NullBondTypeKeepsDefaultCalculatorBehavior()
    {
        Assert.That(factory.Create(null), Is.TypeOf<DefaultPositionCalculator>());
    }
}
