namespace CircleTools.Ui.Account.Auth;

/// <summary>
/// Session events a shell tells the user about (FR-008, ADR-0079). Raised by the session classes, which
/// have no UI of their own; the app shell shows a short notice.
/// </summary>
/// <remarks>
/// A notice can be raised before any shell listens: the startup session restore runs before the first
/// render. It is then kept until a listener takes it (<see cref="TakePending"/>).
/// </remarks>
public sealed class SessionNotices
{
    private bool _pending;

    /// <summary>
    /// Raised when the server refused this device's session: it was ended elsewhere ("sign out on all
    /// devices", a password change) or ran out. The device has already been signed out locally.
    /// </summary>
    public event Action? SignedOutByServer;

    /// <summary>Raises <see cref="SignedOutByServer"/>, or keeps the notice until a listener takes it.</summary>
    public void RaiseSignedOutByServer()
    {
        if (SignedOutByServer is { } handler)
        {
            handler();
            return;
        }

        _pending = true;
    }

    /// <summary>Takes a notice raised while nobody listened, so it is shown once.</summary>
    /// <returns>Whether a sign-out by the server is still to be shown.</returns>
    public bool TakePending()
    {
        var pending = _pending;
        _pending = false;
        return pending;
    }
}
