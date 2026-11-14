// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Circles.Core.Test.TestSupport;

/// <summary>
/// A clock the tests move by hand. The fourteen-day expiry and the seven-day cooldown are advanced
/// virtually — nothing here waits for real time to pass.
/// </summary>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public void Advance(TimeSpan by) => Now += by;

    public override DateTimeOffset GetUtcNow() => Now;
}
