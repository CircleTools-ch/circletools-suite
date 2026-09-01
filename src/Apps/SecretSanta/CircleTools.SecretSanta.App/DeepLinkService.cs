namespace CircleTools.SecretSanta.App;

/// <summary>
/// Carries an incoming reveal deep link (ADR-0053) from the platform activation into the Blazor app.
/// </summary>
/// <remarks>
/// Android delivers an App Link (<c>https://secretsanta.circletools.ch/r#…</c>) to <c>MainActivity</c>,
/// which forwards it here. When the app is already running a subscriber (<c>DeepLinkListener</c>)
/// navigates immediately; on a cold start there is no subscriber yet, so the link is held until
/// <see cref="ConsumePending"/> runs once the Blazor tree is ready. The reveal renders from the app's
/// own baked-in UI, entirely offline — no server is involved (ADR-0014).
/// </remarks>
public sealed class DeepLinkService
{
    private readonly Lock _gate = new();
    private string? _pending;

    /// <summary>Raised with an app-relative URL (e.g. <c>r#for=…</c>) when a link arrives while running.</summary>
    public event Action<string>? LinkReceived;

    /// <summary>Forwards a full incoming URL from the platform; ignored if it is not parseable.</summary>
    public void ReceiveAbsolute(string absoluteUrl)
    {
        if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri))
        {
            return;
        }

        // Keep only what the in-app router needs: the path (minus the leading slash) and the fragment
        // that carries the reveal payload. The host is the public origin, not the WebView's own.
        Receive(uri.PathAndQuery.TrimStart('/') + uri.Fragment);
    }

    private void Receive(string relativeUrl)
    {
        var handler = LinkReceived;
        if (handler is not null)
        {
            handler(relativeUrl);
            return;
        }

        lock (_gate)
        {
            _pending = relativeUrl;
        }
    }

    /// <summary>Returns and clears a link that arrived before anything was listening (cold start).</summary>
    public string? ConsumePending()
    {
        lock (_gate)
        {
            var pending = _pending;
            _pending = null;
            return pending;
        }
    }
}
