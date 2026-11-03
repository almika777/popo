using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Popo.Api.Models;
using Popo.Api.Services;
using Popo.Core.Issuers;

namespace Popo.Tests;

public sealed class IssuerLogoManagementServiceTests
{
    private static readonly byte[] Png = [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52,
        0, 0, 0, 1, 0, 0, 0, 1
    ];

    [Test]
    public async Task UploadAsync_SavesOneLogoForSelectedEmitterAndShowsItsAvailability()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var issuerProvider = new FakeIssuerProvider([new IssuerLogoIssuer(11004, "МГКЛ")]);
            var fileStore = new IssuerLogoFileStore(directory);
            var service = new IssuerLogoManagementService(issuerProvider, fileStore);

            var beforeUpload = await service.GetIssuersAsync(CancellationToken.None);
            var uploaded = await service.UploadAsync(new IssuerLogoUploadRequest
            {
                EmitterId = 11004,
                File = new FormFile(new MemoryStream(Png), 0, Png.Length, "file", "mgkl.png")
            }, CancellationToken.None);
            var afterUpload = await service.GetIssuersAsync(CancellationToken.None);
            var image = await service.GetLogoAsync(11004, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(beforeUpload.Single().HasLogo, Is.False);
                Assert.That(uploaded, Is.True);
                Assert.That(afterUpload.Single(), Is.EqualTo(new IssuerLogoListItem(11004, "МГКЛ", true)));
                Assert.That(image?.ContentType, Is.EqualTo("image/png"));
                Assert.That(image?.Content, Is.EqualTo(Png));
            });
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task UploadAsync_DoesNotStoreLogoForUnknownEmitter()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var service = new IssuerLogoManagementService(
                new FakeIssuerProvider([]),
                new IssuerLogoFileStore(directory));

            var uploaded = await service.UploadAsync(new IssuerLogoUploadRequest
            {
                EmitterId = 99999,
                File = new FormFile(new MemoryStream(Png), 0, Png.Length, "file", "logo.png")
            }, CancellationToken.None);

            Assert.That(uploaded, Is.False);
            Assert.That(Directory.Exists(directory), Is.False);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FakeIssuerProvider(IReadOnlyList<IssuerLogoIssuer> issuers) : IIssuerLogoIssuerProvider
    {
        public Task<IReadOnlyList<IssuerLogoIssuer>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult(issuers);

        public Task<bool> ExistsAsync(int emitterId, CancellationToken cancellationToken) =>
            Task.FromResult(issuers.Any(issuer => issuer.EmitterId == emitterId));
    }
}
