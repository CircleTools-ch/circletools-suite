// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>
/// Whether a set of exclusions still leaves a valid draw, checked as the exclusions are edited (Q13a, 2026-10-16
/// device test) — before anyone presses "Draw". The same rules as <see cref="DrawGenerator"/>: nobody draws
/// themselves, exclusions are honoured and no pair draws each other; the "not identical to last time" rule is
/// left out, since that only constrains a re-draw. Pure and deterministic.
/// </summary>
public static class DrawFeasibility
{
    // A search budget, so an unusually large, loosely constrained group never stalls the page: past it the
    // answer is "unknown" and no warning is shown (the draw itself still reports a failure).
    private const int MaxSteps = 200_000;

    /// <summary>Checks the members and exclusions.</summary>
    /// <param name="memberIds">The members taking part.</param>
    /// <param name="exclusions">The exclusions in force.</param>
    /// <returns>Who is left without a recipient or a giver, and whether a draw is possible.</returns>
    public static DrawFeasibilityResult Check(IReadOnlyList<Guid> memberIds, IReadOnlyList<Exclusion> exclusions)
    {
        ArgumentNullException.ThrowIfNull(memberIds);
        ArgumentNullException.ThrowIfNull(exclusions);

        if (memberIds.Count < 3)
        {
            // Too few to judge here: the draw page explains two (or fewer) members on its own.
            return new DrawFeasibilityResult([], [], Possible: null);
        }

        bool Allowed(Guid giver, Guid recipient) =>
            giver != recipient && !exclusions.Any(e => e.Forbids(giver, recipient));

        var options = memberIds.ToDictionary(g => g, g => memberIds.Where(r => Allowed(g, r)).ToList());
        var noRecipient = memberIds.Where(g => options[g].Count == 0).ToList();
        var noGiver = memberIds.Where(r => !memberIds.Any(g => Allowed(g, r))).ToList();
        if (noRecipient.Count > 0 || noGiver.Count > 0)
        {
            return new DrawFeasibilityResult(noRecipient, noGiver, Possible: false);
        }

        // Most constrained givers first keeps the search small.
        var givers = memberIds.OrderBy(g => options[g].Count).ToList();
        var map = new Dictionary<Guid, Guid>();
        var taken = new HashSet<Guid>();
        var steps = 0;

        bool? Search(int index)
        {
            if (index == givers.Count)
            {
                return true;
            }

            var giver = givers[index];
            foreach (var recipient in options[giver])
            {
                if (++steps > MaxSteps)
                {
                    return null;
                }

                // Taken, or the recipient already gives to this giver (a reciprocal pair).
                if (taken.Contains(recipient) || (map.TryGetValue(recipient, out var back) && back == giver))
                {
                    continue;
                }

                map[giver] = recipient;
                taken.Add(recipient);
                var found = Search(index + 1);
                if (found != false)
                {
                    return found;
                }

                map.Remove(giver);
                taken.Remove(recipient);
            }

            return false;
        }

        return new DrawFeasibilityResult([], [], Search(0));
    }
}

/// <summary>The outcome of <see cref="DrawFeasibility.Check"/>.</summary>
/// <param name="WithoutRecipient">Members every other member is excluded for as a recipient.</param>
/// <param name="WithoutGiver">Members nobody may give to.</param>
/// <param name="Possible">Whether a draw exists; <see langword="null"/> when not judged (too few members, or the search was cut short).</param>
public sealed record DrawFeasibilityResult(IReadOnlyList<Guid> WithoutRecipient, IReadOnlyList<Guid> WithoutGiver, bool? Possible);
