using System.Buffers.Text;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AppSuite.SecretSanta.Contracts;

namespace AppSuite.SecretSanta.Core.Reveal;

/// <summary>
/// Encodes and decodes a <see cref="RevealPayload"/> to and from a compact, URL-safe blob
/// (ADR-0014): <c>System.Text.Json</c> → <c>Deflate</c> → <c>Base64Url</c>. All primitives are
/// in-framework so the codec runs unchanged in WebAssembly. This is encoding, not encryption.
/// </summary>
public sealed class RevealLinkCodec
{
    private readonly JsonTypeInfo<RevealPayload> _typeInfo = RevealJsonContext.Default.RevealPayload;

    /// <summary>Encodes a payload into a URL-safe blob.</summary>
    /// <param name="payload">The payload to encode.</param>
    /// <returns>The Base64Url-encoded, deflate-compressed JSON.</returns>
    public string Encode(RevealPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var json = JsonSerializer.SerializeToUtf8Bytes(payload, _typeInfo);

        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(json, 0, json.Length);
        }

        return Base64Url.EncodeToString(output.ToArray());
    }

    /// <summary>Decodes a blob produced by <see cref="Encode"/> back into a payload.</summary>
    /// <param name="blob">The Base64Url blob from the reveal link fragment.</param>
    /// <returns>The decoded payload.</returns>
    /// <exception cref="FormatException">Thrown when the blob is malformed or cannot be decoded.</exception>
    public RevealPayload Decode(string blob)
    {
        if (string.IsNullOrWhiteSpace(blob))
        {
            throw new FormatException("The reveal payload is empty.");
        }

        try
        {
            var compressed = Base64Url.DecodeFromChars(blob);

            using var input = new MemoryStream(compressed);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);

            var payload = JsonSerializer.Deserialize(output.ToArray(), _typeInfo);
            return payload ?? throw new FormatException("The reveal payload could not be decoded.");
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidDataException or InvalidOperationException)
        {
            throw new FormatException("The reveal link is malformed.", exception);
        }
    }
}
