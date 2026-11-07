using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Popo.Core.Issuers;
using Popo.Storage.Entities.Moex;
using Popo.Storage.Providers;

namespace Popo.Storage.Tests;

[TestFixture]
public sealed class IssuerLogoIssuerProviderTests
{
    [Test]
    public async Task GetAllAsync_ReturnsIssuerIdsAndTitlesInTitleOrder()
    {
        await using var database = await TestDatabase.CreateIsolatedAsync();
        await using (var context = await database.ContextFactory.CreateDbContextAsync())
        {
            context.MoexEmitents.AddRange(
                new MoexEmitentEntity { Id = 910002, Title = "Эмитент Б" },
                new MoexEmitentEntity { Id = 910001, Title = "Эмитент А" });
            await context.SaveChangesAsync();
        }

        var provider = new IssuerLogoIssuerProvider(database.ContextFactory);

        var result = await provider.GetAllAsync(CancellationToken.None);

        Assert.That(result.Take(2), Is.EqualTo(new[]
        {
            new IssuerLogoIssuer(910001, "Эмитент А"),
            new IssuerLogoIssuer(910002, "Эмитент Б")
        }));
        Assert.That(await provider.ExistsAsync(910001, CancellationToken.None), Is.True);
        Assert.That(await provider.ExistsAsync(919999, CancellationToken.None), Is.False);
    }
}
