namespace AppSuite.SecretSanta.Core.Domain;

/// <summary>
/// Domain service that produces a Secret-Santa draw: a derangement (no one draws themselves)
/// that additionally satisfies (a) the configured exclusions, (b) no reciprocal pair
/// (A → B implies not B → A), and (c) is not identical to the previous draw.
/// </summary>
/// <remarks>
/// Uses a bounded randomized retry: it repeatedly shuffles candidate recipients (via the
/// injected <see cref="IRandomSource"/>) until a valid assignment is found or the attempt
/// budget is exhausted. The result is deterministic for a fixed seed.
/// </remarks>
/// <param name="random">The randomness source used to shuffle candidates.</param>
public sealed class DrawGenerator(IRandomSource random)
{
    /// <summary>The maximum number of randomized attempts before reporting over-constrained.</summary>
    public const int MaxAttempts = 2000;

    /// <summary>Attempts to generate a valid draw for the supplied members.</summary>
    /// <param name="memberIds">The participating member identities.</param>
    /// <param name="exclusions">Directed exclusions (giver must not draw recipient).</param>
    /// <param name="previous">The previous draw as a giver-to-recipient map, or <see langword="null"/>.</param>
    /// <returns>A successful <see cref="DrawResult"/>, or a failure with the reason.</returns>
    public DrawResult Generate(
        IReadOnlyList<Guid> memberIds,
        IReadOnlyList<Exclusion> exclusions,
        IReadOnlyDictionary<Guid, Guid>? previous)
    {
        ArgumentNullException.ThrowIfNull(memberIds);
        ArgumentNullException.ThrowIfNull(exclusions);

        if (memberIds.Count < 2)
        {
            return DrawResult.Failure(DrawFailureReason.OverConstrained);
        }

        // With exactly two members the only derangement is the reciprocal swap, which is disallowed.
        if (memberIds.Count == 2)
        {
            return DrawResult.Failure(DrawFailureReason.TwoMemberReciprocalImpossible);
        }

        var excluded = new HashSet<(Guid Giver, Guid Recipient)>(
            exclusions.Select(e => (e.GiverMemberId, e.RecipientMemberId)));

        var givers = memberIds.ToList();

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var recipients = memberIds.ToList();
            random.Shuffle(recipients);

            if (IsValid(givers, recipients, excluded, previous))
            {
                var assignments = new List<Assignment>(givers.Count);
                for (var i = 0; i < givers.Count; i++)
                {
                    assignments.Add(Assignment.Create(givers[i], recipients[i]));
                }

                return DrawResult.Success(assignments);
            }
        }

        return DrawResult.Failure(DrawFailureReason.OverConstrained);
    }

    private static bool IsValid(
        List<Guid> givers,
        List<Guid> recipients,
        HashSet<(Guid, Guid)> excluded,
        IReadOnlyDictionary<Guid, Guid>? previous)
    {
        var map = new Dictionary<Guid, Guid>(givers.Count);
        for (var i = 0; i < givers.Count; i++)
        {
            var giver = givers[i];
            var recipient = recipients[i];

            // (derangement) no one draws themselves.
            if (giver == recipient)
            {
                return false;
            }

            // (a) respect exclusions.
            if (excluded.Contains((giver, recipient)))
            {
                return false;
            }

            map[giver] = recipient;
        }

        // (b) no reciprocal pair: A → B must not coexist with B → A.
        foreach (var (giver, recipient) in map)
        {
            if (map.TryGetValue(recipient, out var back) && back == giver)
            {
                return false;
            }
        }

        // (c) not identical to the previous draw.
        if (previous is { Count: > 0 } && previous.Count == map.Count &&
            map.All(pair => previous.TryGetValue(pair.Key, out var r) && r == pair.Value))
        {
            return false;
        }

        return true;
    }
}
