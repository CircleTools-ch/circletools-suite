// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Main.Service;

/// <summary>
/// Marks a route as answering in constant time, and names the budget it is held to (NFR-005).
/// </summary>
/// <param name="Budget">The fixed duration every response takes, however little work it did.</param>
/// <remarks>
/// Metadata rather than a filter constructor argument, for the same reason as
/// <see cref="CircleRoleRequirement"/>: it can be read back off the endpoint data source, so a
/// fitness function can assert that every address-accepting route carries one.
/// </remarks>
public sealed record ConstantTimeRequirement(TimeSpan Budget);

/// <summary>
/// Holds a response to a fixed duration, so the work behind it cannot be timed (NFR-005).
/// </summary>
/// <param name="clock">The application clock, used for both measurement and the wait.</param>
/// <remarks>
/// <para>
/// Several endpoints accept an email address and do markedly more work when it belongs to an account
/// than when it does not: a token is generated and a mail is queued, or neither is. The response body
/// and status are identical — that part was got right — but the duration is not, and a median over a
/// few dozen samples reads it straight off. Measured on <c>/forgot-password</c> before this filter:
/// <strong>2.36 ms against 1.24 ms</strong> on an idle machine, and 7.44 ms against 4.19 ms under
/// load. The ratio held at ~1.9x in both, which is what tells you it is structural and not noise.
/// </para>
/// <para>
/// <strong>Why a fixed budget rather than a decoy.</strong> The established fix in this codebase is
/// <see cref="Identity.Passwords.DecoyPasswordVerifier"/> — spend the same cost on the branch that
/// does not need it. That works for a password hash, which is one call with one cost, and it does not
/// work here: the asymmetry is a token plus a database write, and on <c>/change-email</c> it is a
/// token plus <em>two</em>. Mirroring that means writing rows nobody will read. Worse, the tempting
/// cheap version — spending an unrelated password hash on both paths — buries the difference under a
/// larger constant and makes the ratio look healthy while the absolute gap survives untouched. That
/// defeats the test rather than the leak. A fixed budget removes the signal instead of diluting it.
/// </para>
/// <para>
/// <strong>What it costs and what it assumes.</strong> The wait is asynchronous, so it holds no
/// thread; it does hold the request scope, which is why these routes stay rate-limited. The budget
/// must exceed the slow path under real load or the cap stops capping and the difference reappears.
/// <see cref="DefaultBudget"/> leaves about 7x headroom over the slowest route's typical cost, and
/// <c>ConstantTimeEnvelope.AssertHeldToBudget</c> is what notices if that ever stops being true. It is
/// headroom, not a guarantee: on a host with every core saturated the invite handler measured
/// ~180–230 ms, over budget, and its arms separated again (2026-11-07, backlog PR-16).
/// </para>
/// </remarks>
public sealed class ConstantTimeFilter(TimeProvider clock) : IEndpointFilter
{
    /// <summary>
    /// The budget applied when a route does not name its own.
    /// </summary>
    /// <remarks>
    /// Chosen against measurement, not taste: the slowest of these handlers, the invite form, has a
    /// median cost of 15–21 ms on a busy machine, and sign-in and sign-up already cost ~85 ms because
    /// of the password hash they must pay. A budget in that region gives headroom and has the side
    /// benefit that the address-accepting endpoints stop being distinguishable from each other by
    /// duration either.
    /// Latency is irrelevant on all four: every one of them answers "we have queued a mail".
    /// </remarks>
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromMilliseconds(120);

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        var budget = http.GetEndpoint()?.Metadata.GetMetadata<ConstantTimeRequirement>()?.Budget
            ?? throw new InvalidOperationException(
                "A route was mapped without a ConstantTimeRequirement. Use RequireConstantTime.");

        var started = clock.GetTimestamp();
        var result = await next(context).ConfigureAwait(false);
        var remaining = budget - clock.GetElapsedTime(started);

        if (remaining > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(remaining, clock, http.RequestAborted).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The caller hung up mid-wait. They cannot have timed a response they did not
                // receive, so there is nothing to protect and nothing worth logging.
            }
        }

        return result;
    }
}

/// <summary>Applies <see cref="ConstantTimeFilter"/> to a route.</summary>
public static class ConstantTimeFilterExtensions
{
    /// <summary>
    /// Holds the route to a constant response time (NFR-005).
    /// </summary>
    /// <param name="builder">The route being mapped.</param>
    /// <param name="budget">The budget, or <see langword="null"/> for
    /// <see cref="ConstantTimeFilter.DefaultBudget"/>.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// Chain this <strong>before</strong> any other filter on the route. Endpoint filters nest in
    /// registration order, so calling it first makes it the outermost — which is the only position
    /// from which it covers the work the inner filters do as well.
    /// </remarks>
    public static RouteHandlerBuilder RequireConstantTime(
        this RouteHandlerBuilder builder,
        TimeSpan? budget = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithMetadata(new ConstantTimeRequirement(budget ?? ConstantTimeFilter.DefaultBudget))
            .AddEndpointFilter<ConstantTimeFilter>();
    }
}
