// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>
/// Abstraction over randomness so draw generation can be made deterministic in tests
/// (inject a seeded implementation) while using a strong source in production.
/// </summary>
public interface IRandomSource
{
    /// <summary>Returns a non-negative random integer strictly less than <paramref name="maxExclusive"/>.</summary>
    /// <param name="maxExclusive">The exclusive upper bound; must be positive.</param>
    /// <returns>A random integer in the range [0, <paramref name="maxExclusive"/>).</returns>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "'Next' is the conventional name for a random-number method (matches System.Random.Next).")]
    int Next(int maxExclusive);

    /// <summary>Shuffles the supplied list in place using the Fisher-Yates algorithm.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The list to shuffle.</param>
    void Shuffle<T>(IList<T> items);
}
