using CircleTools.SecretSanta.Ui;

namespace CircleTools.SecretSanta.Ui.Test.TestSupport;

/// <summary>
/// A trivial <see cref="ISyncSignals"/> for component tests: the pages subscribe to
/// <see cref="ISyncSignals.SyncCompleted"/> to refresh on a background sync (FEAT-013 co-admin edits),
/// so a test only needs the event to exist. Call <see cref="RaiseSyncCompleted"/> to assert a page
/// reloads in response.
/// </summary>
public sealed class FakeSyncSignals : ISyncSignals
{
    public event Action? SyncCompleted;

    /// <summary>Fires the signal, as a completed background sync cycle would.</summary>
    public void RaiseSyncCompleted() => SyncCompleted?.Invoke();
}
