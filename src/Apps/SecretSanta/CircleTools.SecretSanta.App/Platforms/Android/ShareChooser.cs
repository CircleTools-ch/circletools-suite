using Android.App;
using Android.Content;
using Android.OS;
using CircleTools.Ui.Abstractions;
using Microsoft.Maui.ApplicationModel;
using MauiPlatform = Microsoft.Maui.ApplicationModel.Platform;

namespace CircleTools.SecretSanta.App;

/// <summary>
/// The Android share sheet with a "target chosen" callback (D5, 2026-10-15 sync test), so the Share page
/// counts a reveal link as handed out only when it really went to an app. MAUI's <c>Share</c> API can't
/// tell a chosen target from a dismissed sheet.
/// </summary>
/// <remarks>
/// <c>Intent.createChooser</c> with an <see cref="IntentSender"/> broadcasts to
/// <see cref="ShareTargetChosenReceiver"/> when the user picks a target. A dismissed sheet sends nothing, so
/// the outcome is "dismissed" once this activity is back in front and no broadcast arrived shortly after.
/// </remarks>
internal static class ShareChooser
{
    private static readonly TimeSpan BroadcastGrace = TimeSpan.FromMilliseconds(600);

    /// <summary>Shows the chooser for a plain-text share and reports whether a target was chosen.</summary>
    /// <param name="title">The chooser title and e-mail subject.</param>
    /// <param name="text">The text to share.</param>
    /// <returns>The outcome.</returns>
    public static async Task<ShareOutcome> ShareAsync(string title, string text)
    {
        var activity = MauiPlatform.CurrentActivity;
        if (activity is null)
        {
            await Share.Default.RequestAsync(new ShareTextRequest { Title = title, Text = text });
            return ShareOutcome.Unknown;
        }

        var send = new Intent(Intent.ActionSend);
        send.SetType("text/plain");
        send.PutExtra(Intent.ExtraText, text);
        send.PutExtra(Intent.ExtraSubject, title);

        // Explicit (our own receiver), so a mutable PendingIntent is allowed; mutable so the system can add
        // the chosen component to it.
        var callback = new Intent(activity, typeof(ShareTargetChosenReceiver));
        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            flags |= PendingIntentFlags.Mutable;
        }

        using var pending = PendingIntent.GetBroadcast(activity, 0x5A17, callback, flags);
        var chooser = Intent.CreateChooser(send, title, pending!.IntentSender);

        var chosen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var paused = false;

        void OnChosen() => chosen.TrySetResult(true);
        void OnActivityState(object? sender, ActivityStateChangedEventArgs e)
        {
            if (e.Activity != activity)
            {
                return;
            }

            if (e.State is ActivityState.Paused or ActivityState.Stopped)
            {
                paused = true;
            }
            else if (e.State == ActivityState.Resumed && paused)
            {
                resumed.TrySetResult(true);
            }
        }

        ShareTargetChosenReceiver.Chosen += OnChosen;
        MauiPlatform.ActivityStateChanged += OnActivityState;
        try
        {
            activity.StartActivity(chooser);

            var first = await Task.WhenAny(chosen.Task, resumed.Task);
            if (first == chosen.Task)
            {
                return ShareOutcome.Shared;
            }

            // Back in front with no broadcast yet: give a late broadcast a moment, then call it dismissed.
            var late = await Task.WhenAny(chosen.Task, Task.Delay(BroadcastGrace));
            return late == chosen.Task ? ShareOutcome.Shared : ShareOutcome.Dismissed;
        }
        finally
        {
            ShareTargetChosenReceiver.Chosen -= OnChosen;
            MauiPlatform.ActivityStateChanged -= OnActivityState;
        }
    }
}

/// <summary>Receives the chooser's "a target was chosen" broadcast for <see cref="ShareChooser"/>.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class ShareTargetChosenReceiver : BroadcastReceiver
{
    /// <summary>Raised when the user picked a share target.</summary>
    public static event Action? Chosen;

    /// <inheritdoc />
    public override void OnReceive(Context? context, Intent? intent) => Chosen?.Invoke();
}
