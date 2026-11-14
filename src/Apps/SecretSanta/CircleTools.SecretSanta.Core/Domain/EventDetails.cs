// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SharedKernel;

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>Value object describing the gift-exchange event (date, location, description).</summary>
public sealed class EventDetails : ValueObject
{
    private EventDetails(DateTimeOffset? date, string location, string description)
    {
        Date = date;
        Location = location;
        Description = description;
    }

    /// <summary>Gets the optional event date and time.</summary>
    public DateTimeOffset? Date { get; }

    /// <summary>Gets the event location (possibly empty).</summary>
    public string Location { get; }

    /// <summary>Gets the free-text description (possibly empty).</summary>
    public string Description { get; }

    /// <summary>Creates an <see cref="EventDetails"/> value.</summary>
    /// <param name="date">The optional event date.</param>
    /// <param name="location">The location; may be null or empty.</param>
    /// <param name="description">The description; may be null or empty.</param>
    /// <returns>The created event details.</returns>
    public static EventDetails Create(DateTimeOffset? date, string? location, string? description) =>
        new(date, (location ?? string.Empty).Trim(), (description ?? string.Empty).Trim());

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Date;
        yield return Location;
        yield return Description;
    }
}
