// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using CircleTools.SecretSanta.Ui;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// The "Change saved · Undo" offer hides by itself (R13, 2026-10-20 device test): MudBlazor keeps a snackbar
/// with an action open until closed by hand unless told otherwise, and the offer sat over the app bar for minutes.
/// </summary>
public sealed class UndoLastEditTests : IAsyncDisposable
{
    private readonly Bunit.BunitContext _ctx = new();

    [Fact]
    public void TheUndoOffer_HidesByItself_AfterAFewSeconds()
    {
        _ctx.Services.AddMudServices();
        var snackbar = CapturingSnackbar.Wrap(_ctx.Services.GetRequiredService<ISnackbar>(), out var capture);

        UndoLastEdit.Offer(snackbar, new PassThroughStringLocalizer<AppStrings>(), () => Task.CompletedTask);

        var options = new SnackbarOptions(Severity.Normal, new SnackbarConfiguration());
        Assert.NotNull(capture.Configure);
        capture.Configure(options);
        Assert.Equal("Undo", options.Action);
        Assert.False(options.RequireInteraction);
        Assert.InRange(options.VisibleStateDuration, 1000, 10000);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();
}

/// <summary>Passes every call to a real snackbar service and keeps the options callback of the last Add.</summary>
public class CapturingSnackbar : DispatchProxy
{
    private ISnackbar? _inner;

    /// <summary>The options callback the last <c>Add</c> was given.</summary>
    public Action<SnackbarOptions>? Configure { get; private set; }

    /// <summary>Wraps a snackbar service.</summary>
    /// <param name="inner">The real service.</param>
    /// <param name="capture">The proxy, to read what was captured.</param>
    /// <returns>The wrapping <see cref="ISnackbar"/>.</returns>
    public static ISnackbar Wrap(ISnackbar inner, out CapturingSnackbar capture)
    {
        var proxy = Create<ISnackbar, CapturingSnackbar>();
        capture = (CapturingSnackbar)(object)proxy;
        capture._inner = inner;
        return proxy;
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        if (targetMethod.Name == nameof(ISnackbar.Add) && args?.OfType<Action<SnackbarOptions>>().FirstOrDefault() is { } configure)
        {
            Configure = configure;
        }

        return targetMethod.Invoke(_inner, args);
    }
}
