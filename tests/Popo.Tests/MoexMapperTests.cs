using NUnit.Framework;
using Popo.HttpClients.Common;
using Popo.HttpClients.Contracts.Iss;

namespace Popo.Tests;

public sealed class MoexMapperTests
{
    [Test]
    public void Map_converts_moex_description_fields_to_typed_values()
    {
        var mapped = MoexMapper.Map<MoexBondDescription>(new Dictionary<string, string?>
        {
            ["SECID"] = "RU000A",
            ["MATDATE"] = "2030-12-31",
            ["FACEVALUE"] = "1000.5",
            ["HASDEFAULT"] = "0"
        });

        Assert.Multiple(() =>
        {
            Assert.That(mapped.SecId, Is.EqualTo("RU000A"));
            Assert.That(mapped.MatDate, Is.EqualTo(new DateOnly(2030, 12, 31)));
            Assert.That(mapped.FaceValue, Is.EqualTo(1000.5));
            Assert.That(mapped.HasDefault, Is.False);
        });
    }
}
