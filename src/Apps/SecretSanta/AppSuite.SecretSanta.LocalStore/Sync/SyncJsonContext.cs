using System.Text.Json.Serialization;
using AppSuite.Main.Contracts;
using AppSuite.SecretSanta.Core.Persistence;

namespace AppSuite.SecretSanta.LocalStore;

/// <summary>
/// Source-generated JSON for the client sync engine (FEAT-004). Camel-cased to match what the API
/// speaks, so a group body serialized here round-trips through the server — which stores it as an
/// opaque document — and is understood identically on another device.
/// </summary>
/// <remarks>
/// Source generation rather than reflection because the Secret-Santa web app is a trimmed Blazor
/// WebAssembly build; reflection-based serialization of these records would be trimmed away.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SyncGroupDocument))]
[JsonSerializable(typeof(SyncState))]
[JsonSerializable(typeof(GroupState))]
[JsonSerializable(typeof(SyncRequest))]
[JsonSerializable(typeof(SyncResponse))]
[JsonSerializable(typeof(ClaimGroupRequest))]
public sealed partial class SyncJsonContext : JsonSerializerContext;
