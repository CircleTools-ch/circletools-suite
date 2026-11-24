// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>One response from a route under the constant-time envelope.</summary>
/// <param name="TotalMs">What the caller measured: the only thing an attacker sees.</param>
/// <param name="HandlerMs">
/// What the handler behind the filter cost, which the envelope exists to hide. Equal to the budget
/// when the handler used all of it, because the filter then has nothing left to wait for.
/// </param>
public readonly record struct ConstantTimeSample(double TotalMs, double HandlerMs);

/// <summary>
/// The application clock, recording the wait <see cref="ConstantTimeFilter"/> asks it for.
/// </summary>
/// <remarks>
/// The filter waits for <c>budget - handler cost</c>, so the requested wait gives the handler's cost
/// to the millisecond (<c>Task.Delay</c> rounds to that), with no timer landing error in it and no
/// production code exposing it. Recording it in the response would be the leak this filter closes.
/// </remarks>
public sealed class ConstantTimeClock : TimeProvider
{
    private const long NoWait = -1;
    private long _lastWaitTicks = NoWait;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (period == Timeout.InfiniteTimeSpan
            && dueTime > TimeSpan.Zero
            && dueTime <= ConstantTimeFilter.DefaultBudget)
        {
            Interlocked.Exchange(ref _lastWaitTicks, dueTime.Ticks);
        }

        return System.CreateTimer(callback, state, dueTime, period);
    }

    /// <summary>Times one request. Requests must not overlap, which a test collection guarantees.</summary>
    /// <param name="call">The request.</param>
    /// <returns>The caller's duration and the handler's cost.</returns>
    public async Task<ConstantTimeSample> MeasureAsync(Func<Task> call)
    {
        ArgumentNullException.ThrowIfNull(call);

        Interlocked.Exchange(ref _lastWaitTicks, NoWait);

        var stopwatch = Stopwatch.StartNew();
        await call();
        stopwatch.Stop();

        var waitTicks = Interlocked.Read(ref _lastWaitTicks);
        var budget = ConstantTimeFilter.DefaultBudget;
        var handler = waitTicks == NoWait ? budget : budget - TimeSpan.FromTicks(waitTicks);

        return new ConstantTimeSample(stopwatch.Elapsed.TotalMilliseconds, handler.TotalMilliseconds);
    }
}

/// <summary>
/// Asserts that a route carrying <c>RequireConstantTime</c> holds its NFR-005 target: every path
/// reaches the budget, and the budget covers every path.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why not "the paths are within timer jitter of each other".</strong> This suite asserted
/// that until 2026-11-07, at 2 ms, and it failed on routes with nothing wrong. An A/A control sending
/// the <em>identical</em> request as separate arms failed it as often as the real arms did: 2/20
/// against 3/20 runs on a machine at ~40 % CPU, 2/15 against 4/15 beside a test run. The reason is
/// the instrument. On Windows <c>Task.Delay</c> lands a median of ~3.9 ms late and 6–14 ms late at
/// p95, so the fastest of 10 samples is itself a noisy number, and the handler costs that really do
/// differ (~2 ms) never reach the caller: all three arms' medians agreed within 0.06 ms.
/// </para>
/// <para>
/// <strong>What replaces it asks the actual question directly.</strong> The old check was a proxy for
/// "has the budget stopped covering the slowest arm?". The handler's cost can be read off the wait
/// the filter requests, without a timer in the way, so that is now asserted itself. It also notices
/// sooner: a slow arm only moved the old statistic once it passed the budget and the difference was
/// already leaking, while this fires with half the budget still in hand.
/// </para>
/// </remarks>
public static class ConstantTimeEnvelope
{
    /// <summary>
    /// How far below its budget a response may land before something other than the timer is
    /// responsible.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Task.Delay</c> lands early on Linux: the timer queue runs on a coarse tick and the delay is
    /// truncated to whole milliseconds. Measured: up to 1.27 ms early on Windows; 2.04 ms and 2.12 ms
    /// on the Ubuntu CI runners (2026-11-20 and 2026-11-24, which failed the old 2 ms tolerance on
    /// routes with nothing wrong); and up to 9.76 ms early under WSL2 (fastest of 400 waits for a
    /// 120 ms budget, 3 of them more than 2 ms early). 15 ms covers all of them with room.
    /// </para>
    /// <para>
    /// Not a leak tolerance: losing the filter misses this by ~100 ms, because the handlers behind it
    /// cost 1–2 ms. The early landing is the timer's, so it does not depend on the path taken.
    /// </para>
    /// </remarks>
    private static readonly TimeSpan TimerEarlyLanding = TimeSpan.FromMilliseconds(15);

    /// <summary>
    /// How far apart the paths' fastest responses may sit, as seen by the caller.
    /// </summary>
    /// <remarks>
    /// Identical requests measured up to 4.64 ms apart on a busy machine, and beside a test run no pair
    /// of paths passed 3.10 ms. On a machine with room to spare, a difference over this limit has to
    /// come from outside the filter, since the filter absorbs everything inside it; the handler
    /// assertion, not this one, guards the work inside. With every core saturated this limit is not
    /// reliable either (15.25 ms was seen once, on a route with nothing wrong).
    /// </remarks>
    private static readonly TimeSpan PathSpreadLimit = TimeSpan.FromMilliseconds(10);

    /// <summary>The share of the budget a path's cheapest handler run may use.</summary>
    /// <remarks>
    /// The cheapest run, because load can only add to it. Half, because a handler's p95 measured 5–10x
    /// its cheapest run: at half the budget, ordinary spikes already overrun it and the paths separate
    /// in those samples. Measured for comparison: at most 26 ms beside a test run, against this 60 ms.
    /// </remarks>
    private const double MaxHandlerShareOfBudget = 0.5;

    /// <summary>Fails unless the route is held to its budget on every path.</summary>
    /// <param name="surface">The endpoint under test, for the failure message.</param>
    /// <param name="paths">The samples per path, measured interleaved.</param>
    public static void AssertHeldToBudget(
        string surface,
        IReadOnlyDictionary<string, List<ConstantTimeSample>> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var budget = ConstantTimeFilter.DefaultBudget.TotalMilliseconds;
        var fastest = paths.ToDictionary(path => path.Key, path => path.Value.Min(sample => sample.TotalMs));
        var cheapest = paths.ToDictionary(path => path.Key, path => path.Value.Min(sample => sample.HandlerMs));
        var report = string.Join(
            ". ",
            paths.Keys.Select(path => $"{path}: {fastest[path]:F2} ms total, {cheapest[path]:F2} ms in the handler"));

        var lowest = fastest.MinBy(path => path.Value);
        var highest = fastest.MaxBy(path => path.Value);

        // The minimum, because the budget is a floor: load pushes samples above it and nothing pushes
        // them below, so it is the one statistic a busy machine cannot inflate.
        Assert.True(
            lowest.Value >= budget - TimerEarlyLanding.TotalMilliseconds,
            $"NFR-005: {surface} is not being held to its constant-time budget. {report}. The budget is "
            + $"{budget:F0} ms, and '{lowest.Key}' came in under it. Either the route lost its "
            + "RequireConstantTime, or the filter is no longer running outermost, and the work behind "
            + "it is timeable again.");

        var handlerLimit = budget * MaxHandlerShareOfBudget;
        var costliest = cheapest.MaxBy(path => path.Value);

        Assert.True(
            costliest.Value <= handlerLimit,
            $"NFR-005: {surface} has outgrown its constant-time budget. {report}. Even the cheapest "
            + $"'{costliest.Key}' handler run took {costliest.Value:F2} ms against a limit of "
            + $"{handlerLimit:F0} ms (half of the {budget:F0} ms budget), so the budget no longer reliably "
            + "covers this path. Raise ConstantTimeFilter.DefaultBudget or make the handler cheaper. "
            + "First check whether every core on this machine was busy: there the invite form measures "
            + "135+ ms, and the budget really does stop covering it (backlog PR-16). A saturated run "
            + "says nothing about a code change, so rerun it with room to spare before acting.");

        var spread = highest.Value - lowest.Value;

        Assert.True(
            spread <= PathSpreadLimit.TotalMilliseconds,
            $"NFR-005: {surface} diverges under its budget by more than timer noise. {report}. "
            + $"'{highest.Key}' and '{lowest.Key}' are {spread:F2} ms apart while the handlers fit the "
            + "budget. Unless every core on this machine was busy, which this limit does not survive "
            + "(backlog PR-16), a path pays for something outside the filter. Find that work and move "
            + "it inside; raising the budget will not cover it.");
    }
}
