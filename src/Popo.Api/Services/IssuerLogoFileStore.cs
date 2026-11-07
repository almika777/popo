using System.Xml;
using System.Xml.Linq;
using System.Buffers.Binary;
using Popo.Api.Validators;

namespace Popo.Api.Services;

public sealed record IssuerLogoImage(byte[] Content, string ContentType);

public sealed class IssuerLogoFileStore
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private const string SvgNamespace = "http://www.w3.org/2000/svg";
    private readonly string _directory;

    public IssuerLogoFileStore(IConfiguration configuration, IHostEnvironment environment)
        : this(ResolveDirectory(configuration["IssuerLogos:Directory"], environment.ContentRootPath))
    {
    }

    public IssuerLogoFileStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
    }

    public bool HasLogo(int emitterId) => GetImagePath(emitterId) is not null;

    public async Task<IssuerLogoImage?> GetAsync(int emitterId, CancellationToken cancellationToken)
    {
        var imagePath = GetImagePath(emitterId);
        if (imagePath is null)
            return null;

        var extension = Path.GetExtension(imagePath);
        return new IssuerLogoImage(
            await File.ReadAllBytesAsync(imagePath, cancellationToken),
            extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ? "image/svg+xml" : "image/png");
    }

    public async Task SaveAsync(int emitterId, string extension, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (emitterId <= 0)
            throw new ArgumentOutOfRangeException(nameof(emitterId));

        extension = extension.ToLowerInvariant();
        if (extension is not (".png" or ".svg"))
            throw new ArgumentException("Поддерживаются только PNG и SVG.", nameof(extension));

        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await content.ReadAsync(chunk, cancellationToken);
            if (read == 0)
                break;
            if (buffer.Length + read > IssuerLogoUploadRequestValidator.MaximumFileSize)
                throw new InvalidDataException("Размер логотипа не должен превышать 2 МБ.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        var bytes = buffer.ToArray();
        if (!IsValidImage(bytes, extension))
            throw new InvalidDataException("Содержимое файла не соответствует формату PNG или SVG.");

        Directory.CreateDirectory(_directory);
        var filePath = Path.Combine(_directory, $"{emitterId}{extension}");
        var temporaryPath = Path.Combine(_directory, $"{emitterId}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
            File.Move(temporaryPath, filePath, overwrite: true);
            var previousExtension = extension == ".png" ? ".svg" : ".png";
            File.Delete(Path.Combine(_directory, $"{emitterId}{previousExtension}"));
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private string? GetImagePath(int emitterId)
    {
        if (emitterId <= 0)
            return null;

        foreach (var extension in new[] { ".png", ".svg" })
        {
            var path = Path.Combine(_directory, $"{emitterId}{extension}");
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string ResolveDirectory(string? configuredDirectory, string contentRoot)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
            return Path.IsPathRooted(configuredDirectory)
                ? configuredDirectory
                : Path.GetFullPath(configuredDirectory, contentRoot);

        for (DirectoryInfo? directory = new(contentRoot); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "emitter_logos");
            if (Directory.Exists(candidate))
                return candidate;
        }

        return Path.Combine(contentRoot, "emitter_logos");
    }

    public static bool IsValidImage(byte[] content, string extension)
    {
        if (extension == ".png")
        {
            return content.Length >= 24
                && content.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature)
                && content.AsSpan(12, 4).SequenceEqual("IHDR"u8)
                && BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(16, 4)) > 0
                && BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(20, 4)) > 0;
        }

        try
        {
            using var stream = new MemoryStream(content);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var root = XDocument.Load(reader).Root;
            if (root?.Name.LocalName != "svg" || root.Name.NamespaceName != SvgNamespace)
                return false;

            return root.DescendantsAndSelf().All(element =>
                element.Name.LocalName is not ("script" or "foreignObject")
                && element.Attributes().All(attribute =>
                    !attribute.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                    && (attribute.Name.LocalName is not ("href" or "style")
                        || IsSafeSvgAttribute(attribute.Name.LocalName, attribute.Value))));
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static bool IsSafeSvgAttribute(string name, string value)
    {
        if (name == "href")
            return value.Length == 0 || value.StartsWith('#');

        return !value.Contains("url(", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("@import", StringComparison.OrdinalIgnoreCase);
    }
}
