using System.Text.Json.Serialization;
using CircleTools.Main.Contracts;
using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.SecretSanta.LocalStore;

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
[JsonSerializable(typeof(SyncedProfileRecord))]
[JsonSerializable(typeof(SyncedPreferencesRecord))]
[JsonSerializable(typeof(PreferencesState))]
[JsonSerializable(typeof(GroupState))]
[JsonSerializable(typeof(SyncRequest))]
[JsonSerializable(typeof(SyncResponse))]
[JsonSerializable(typeof(ClaimGroupRequest))]
[JsonSerializable(typeof(CreateSharedGroupRequest))]
[JsonSerializable(typeof(PushSharedGroupRequest))]
[JsonSerializable(typeof(PushSharedGroupResult))]
[JsonSerializable(typeof(MintedLinks))]
[JsonSerializable(typeof(SharedGroupView))]
[JsonSerializable(typeof(RotatedAdminLink))]
[JsonSerializable(typeof(ClaimedMembers))]
[JsonSerializable(typeof(ClaimPersonRequest))]
[JsonSerializable(typeof(PersonClaimResponse))]
[JsonSerializable(typeof(OwnedSharedGroups))]
[JsonSerializable(typeof(PublishRevealSlicesRequest))]
[JsonSerializable(typeof(PublishRevealSlicesResponse))]
[JsonSerializable(typeof(RevealSlice))]
[JsonSerializable(typeof(RevealSliceView))]
[JsonSerializable(typeof(RevealEntryView))]
[JsonSerializable(typeof(RevealEntryWrite))]
[JsonSerializable(typeof(ParticipantEntry))]
[JsonSerializable(typeof(ParticipantEntryResult))]
[JsonSerializable(typeof(ParticipantKeyRequest))]
[JsonSerializable(typeof(ParticipantKeyResponse))]
[JsonSerializable(typeof(JoinedSharedGroups))]
public sealed partial class SyncJsonContext : JsonSerializerContext;
