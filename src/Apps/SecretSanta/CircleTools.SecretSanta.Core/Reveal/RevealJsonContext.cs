using System.Text.Json.Serialization;
using CircleTools.SecretSanta.Contracts;

namespace CircleTools.SecretSanta.Core.Reveal;

/// <summary>
/// Source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> for the
/// reveal payload, so encoding/decoding is trimming- and AOT-safe in WebAssembly.
/// </summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RevealPayload))]
public sealed partial class RevealJsonContext : JsonSerializerContext;
