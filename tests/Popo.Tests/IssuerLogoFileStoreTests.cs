using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Popo.Api.Services;
using Popo.Api.Models;
using Popo.Api.Validators;

namespace Popo.Tests;

public sealed class IssuerLogoFileStoreTests
{
    private static readonly byte[] Png = [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52,
        0, 0, 0, 1, 0, 0, 0, 1
    ];
    private const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1 1\"><circle cx=\".5\" cy=\".5\" r=\".5\"/></svg>";

    [Test]
    public async Task DefaultDirectory_FindsAndReadsTheExistingEmitterLogo()
    {
        var builder = WebApplication.CreateBuilder();
        var store = new IssuerLogoFileStore(builder.Configuration, builder.Environment);
        var image = await store.GetAsync(11004, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(store.HasLogo(11004), Is.True);
            Assert.That(image?.ContentType, Is.EqualTo("image/png"));
            Assert.That(image?.Content.Length, Is.EqualTo(8010));
        });
    }

    [Test]
    public async Task SaveAsync_StoresLogoByEmitterIdAndReplacesOldExtension()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new IssuerLogoFileStore(directory);
            await store.SaveAsync(11004, ".png", new MemoryStream(Png), CancellationToken.None);
            await store.SaveAsync(11004, ".svg", new MemoryStream(Encoding.UTF8.GetBytes(Svg)), CancellationToken.None);

            var result = await store.GetAsync(11004, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result?.ContentType, Is.EqualTo("image/svg+xml"));
                Assert.That(Encoding.UTF8.GetString(result!.Content), Is.EqualTo(Svg));
                Assert.That(File.Exists(Path.Combine(directory, "11004.png")), Is.False);
                Assert.That(File.Exists(Path.Combine(directory, "11004.svg")), Is.True);
            });
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void SaveAsync_RejectsContentThatDoesNotMatchImageExtension()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new IssuerLogoFileStore(directory);
            Assert.ThrowsAsync<InvalidDataException>(async () =>
                await store.SaveAsync(11004, ".png", new MemoryStream([1, 2, 3]), CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task UploadValidator_AllowsPngAndRejectsUnsupportedExtensionsAndOversizedFiles()
    {
        var validator = new IssuerLogoUploadRequestValidator();
        var valid = await validator.ValidateAsync(new IssuerLogoUploadRequest
        {
            EmitterId = 11004,
            File = CreateFile("logo.png", Png)
        });
        var validSvg = await validator.ValidateAsync(new IssuerLogoUploadRequest
        {
            EmitterId = 11004,
            File = CreateFile("logo.svg", Encoding.UTF8.GetBytes(Svg))
        });
        var unsupported = await validator.ValidateAsync(new IssuerLogoUploadRequest
        {
            EmitterId = 11004,
            File = CreateFile("logo.jpg", Png)
        });
        var oversized = await validator.ValidateAsync(new IssuerLogoUploadRequest
        {
            EmitterId = 11004,
            File = CreateFile("logo.svg", new byte[2 * 1024 * 1024 + 1])
        });

        Assert.Multiple(() =>
        {
            Assert.That(valid.IsValid, Is.True);
            Assert.That(validSvg.IsValid, Is.True);
            Assert.That(unsupported.IsValid, Is.False);
            Assert.That(oversized.IsValid, Is.False);
        });
    }

    [Test]
    public async Task UploadValidator_RejectsSvgWithScript()
    {
        var validator = new IssuerLogoUploadRequestValidator();
        var result = await validator.ValidateAsync(new IssuerLogoUploadRequest
        {
            EmitterId = 11004,
            File = CreateFile(
                "logo.svg",
                Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"))
        });

        Assert.That(result.IsValid, Is.False);
    }

    private static IFormFile CreateFile(string name, byte[] content) => new FormFile(
        new MemoryStream(content), 0, content.Length, "file", name);
}
