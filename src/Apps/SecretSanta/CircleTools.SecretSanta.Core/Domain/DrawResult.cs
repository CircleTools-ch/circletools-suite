// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>The reason a draw could not be produced.</summary>
public enum DrawFailureReason
{
    /// <summary>The exclusions/constraints leave no valid assignment.</summary>
    OverConstrained,

    /// <summary>A two-member group can only be assigned reciprocally, which is disallowed.</summary>
    TwoMemberReciprocalImpossible,
}

/// <summary>Outcome of running the <see cref="DrawGenerator"/>: either the assignments or a failure reason.</summary>
public sealed class DrawResult
{
    private DrawResult(bool isSuccess, IReadOnlyList<Assignment> assignments, DrawFailureReason? failureReason)
    {
        IsSuccess = isSuccess;
        Assignments = assignments;
        FailureReason = failureReason;
    }

    /// <summary>Gets a value indicating whether a valid draw was produced.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the produced assignments (empty when the draw failed).</summary>
    public IReadOnlyList<Assignment> Assignments { get; }

    /// <summary>Gets the failure reason when <see cref="IsSuccess"/> is <see langword="false"/>.</summary>
    public DrawFailureReason? FailureReason { get; }

    /// <summary>Creates a successful result.</summary>
    /// <param name="assignments">The produced assignments.</param>
    /// <returns>A successful <see cref="DrawResult"/>.</returns>
    public static DrawResult Success(IReadOnlyList<Assignment> assignments) => new(true, assignments, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="reason">The reason the draw failed.</param>
    /// <returns>A failed <see cref="DrawResult"/>.</returns>
    public static DrawResult Failure(DrawFailureReason reason) => new(false, [], reason);
}
