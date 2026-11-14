// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;
using CircleTools.SecretSanta.Contracts;

namespace CircleTools.SecretSanta.LocalStore.Online;

/// <summary>
/// Source-generated serializer for the group store's wire DTOs and this device's group records (ADR-0086), so
/// both work on the trimmed WebAssembly head and the fully AOT iOS head (ADR-0072).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CreateOnlineGroupRequest))]
[JsonSerializable(typeof(GroupLinks))]
[JsonSerializable(typeof(OrganizerGroupView))]
[JsonSerializable(typeof(PushOnlineGroupRequest))]
[JsonSerializable(typeof(PushOnlineGroupResponse))]
[JsonSerializable(typeof(JoinLinkRequest))]
[JsonSerializable(typeof(AddParticipantRequest))]
[JsonSerializable(typeof(IReadOnlyList<RevealTokenView>))]
[JsonSerializable(typeof(ParticipantView))]
[JsonSerializable(typeof(OwnEntryRequest))]
[JsonSerializable(typeof(OwnEntryResponse))]
[JsonSerializable(typeof(JoinPreview))]
[JsonSerializable(typeof(JoinRequest))]
[JsonSerializable(typeof(JoinResponse))]
[JsonSerializable(typeof(MyGroups))]
[JsonSerializable(typeof(RevealPayload))]
[JsonSerializable(typeof(RevealEntry))]
[JsonSerializable(typeof(GroupRecordsDocument))]
public sealed partial class OnlineJsonContext : JsonSerializerContext;
