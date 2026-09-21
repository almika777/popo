using System.Text.Json;
using NUnit.Framework;
using Microsoft.EntityFrameworkCore;
using Popo.Storage;
using Popo.Storage.Entities.Moex;
using Popo.HttpClients.Serialization;

namespace Popo.Tests;

public sealed class MoexContractTests
{
    [Test]
    public void OffsetlessMoexDateTime_IsInterpretedAsMoscowAndStoredInUtc()
    {
        var parsed = MoexDateTimeParser.ParseUtc("2026-05-16 10:00:00");

        Assert.That(parsed, Is.EqualTo(new DateTimeOffset(2026, 5, 16, 7, 0, 0, TimeSpan.Zero)));
    }

    [Test]
    public void MoexDateOnly_IsNotShifted()
    {
        var parsed = MoexDateTimeParser.ParseDateOnly("2026-05-16");

        Assert.That(parsed, Is.EqualTo(new DateOnly(2026, 5, 16)));
    }

    [Test]
    public void BondSecurity_primary_key_uses_SecId_and_BoardId()
    {
        using var context = new PopoDbContext(new DbContextOptionsBuilder<PopoDbContext>()
            .UseNpgsql("Host=localhost;Database=popo").Options);
        var entity = context.Model.FindEntityType(typeof(MoexBondSecurityEntity));
        Assert.That(entity!.FindPrimaryKey()!.Properties.Select(x => x.Name),
            Is.EqualTo(new[] { "SecId", "BoardId" }));
    }

    [Test]
    public void MoexJsonConverter_ParsesDateTimeAndPreservesDateOnly()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new MoexDateTimeJsonConverter());
        options.Converters.Add(new MoexDateOnlyJsonConverter());

        var payload = JsonSerializer.Deserialize<ContractPayload>(
            "{\"moment\":\"2026-05-16 10:00:00\",\"tradeDate\":\"2026-05-16\"}", options);

        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Moment, Is.EqualTo(new DateTimeOffset(2026, 5, 16, 7, 0, 0, TimeSpan.Zero)));
        Assert.That(payload.TradeDate, Is.EqualTo(new DateOnly(2026, 5, 16)));
    }

    private sealed class ContractPayload
    {
        [System.Text.Json.Serialization.JsonConverter(typeof(MoexDateTimeJsonConverter))]
        public DateTimeOffset? Moment { get; init; }
        [System.Text.Json.Serialization.JsonConverter(typeof(MoexDateOnlyJsonConverter))]
        public DateOnly? TradeDate { get; init; }
    }
}
