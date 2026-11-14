// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;
using CircleTools.Main.Contracts;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// Source-generated JSON for the account sync (FEAT-004). Camel-cased to match what the API speaks, so a
/// document serialized here round-trips through the server — which stores it opaquely — and is understood
/// identically on another device.
/// </summary>
/// <remarks>
/// Source generation rather than reflection because the Secret-Santa web app is a trimmed Blazor
/// WebAssembly build; reflection-based serialization of these records would be trimmed away.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SyncState))]
[JsonSerializable(typeof(SyncedProfileRecord))]
[JsonSerializable(typeof(SyncedPreferencesRecord))]
[JsonSerializable(typeof(PreferencesState))]
[JsonSerializable(typeof(SyncRequest))]
[JsonSerializable(typeof(SyncResponse))]
public sealed partial class SyncJsonContext : JsonSerializerContext;
