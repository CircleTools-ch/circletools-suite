// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace CircleTools.SecretSanta.Core.Persistence;

/// <summary>
/// The wire and storage format of an online group's document (ADR-0086): the <see cref="GroupState"/> memento
/// as camel-cased JSON. The apps write it and the server reads it through this one definition, so neither side
/// relies on hand-picked property names.
/// </summary>
public static class GroupDocument
{
    /// <summary>
    /// The current document schema version. The server refuses a push whose version is lower than the stored one,
    /// so an older app can't silently drop fields a newer one wrote (ADR-0086, review finding F2).
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Serializes a group memento to the document format.</summary>
    /// <param name="state">The memento.</param>
    /// <returns>The JSON document.</returns>
    public static string Serialize(GroupState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return JsonSerializer.Serialize(state, GroupDocumentJsonContext.Default.GroupState);
    }

    /// <summary>Reads a document back into a group memento.</summary>
    /// <param name="json">The JSON document.</param>
    /// <param name="state">The memento, when the document is well formed.</param>
    /// <returns><see langword="true"/> when the document could be read.</returns>
    public static bool TryDeserialize(string json, out GroupState? state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            state = JsonSerializer.Deserialize(json, GroupDocumentJsonContext.Default.GroupState);
        }
        catch (JsonException)
        {
            return false;
        }

        return state is { Members: not null, Exclusions: not null, Draws: not null };
    }
}

/// <summary>Source-generated serializer for the group document (trimming- and AOT-safe on every head).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GroupState))]
public sealed partial class GroupDocumentJsonContext : JsonSerializerContext;
