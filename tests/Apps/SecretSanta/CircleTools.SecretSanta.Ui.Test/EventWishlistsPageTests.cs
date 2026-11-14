// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.LocalStore;
using CircleTools.SecretSanta.Ui;
using CircleTools.SecretSanta.Ui.Pages;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Abstractions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// The organizer's wishlist autosave (Q29). Regression for the 2026-10-17 browser pass: typing in one
/// member's wishlist and moving to the next field within the typing pause showed "Saved ✓" but stored
/// nothing, and the page kept showing (and would save over) text a background sync had replaced.
/// </summary>
public sealed class EventWishlistsPageTests : IAsyncDisposable
{
    private static readonly TimeSpan PastThePause = TimeSpan.FromSeconds(3);

    private readonly Bunit.BunitContext _ctx = new();
    private readonly WishlistSender _sender = new();
    private readonly FakeSyncSignals _signals = new();

    public EventWishlistsPageTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        _ctx.Services.AddSingleton<ISender>(_sender);
        _ctx.Services.AddSingleton<ISyncSignals>(_signals);
        // The page renders EventBackNav, which records recency (R2-5) via RecentAccessService over storage.
        _ctx.Services.AddSingleton<IKeyValueStore>(new WishlistStubKeyValueStore());
        _ctx.Services.AddSingleton<RecentAccessService>();
        // An organizer's group on this device: nobody takes part, nothing on the server.
    }

    [Fact]
    public void LeavingTheFieldRightAfterTyping_SavesTheTypedText()
    {
        var cut = Render();

        Type(cut, 0, "Book");
        Blur(cut, 0);

        cut.WaitForAssertion(() => Assert.Equal(["Book"], _sender.Stored(WishlistSender.Anna)));
        cut.WaitForAssertion(() => Assert.Equal("SavedCheck", Status(cut, 0)));
    }

    [Fact]
    public void MovingToTheNextMemberWithinThePause_SavesBothMembers()
    {
        var cut = Render();

        Type(cut, 0, "Book");
        Blur(cut, 0);
        Type(cut, 1, "Map\nKnife");
        Blur(cut, 1);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(["Book"], _sender.Stored(WishlistSender.Anna));
            Assert.Equal(["Map", "Knife"], _sender.Stored(WishlistSender.Beat));
        });
    }

    [Fact]
    public void PausingWhileStillInTheField_AutosavesAfterThePause()
    {
        var cut = Render();

        Type(cut, 0, "Book");

        Assert.Equal("Saving", Status(cut, 0));
        cut.WaitForAssertion(() => Assert.Equal(["Book"], _sender.Stored(WishlistSender.Anna)), PastThePause);
        cut.WaitForAssertion(() => Assert.Equal("SavedCheck", Status(cut, 0)), PastThePause);
    }

    [Fact]
    public async Task LeavingThePageWithinThePause_StillSavesTheEdit()
    {
        var cut = Render();

        Type(cut, 0, "Book");
        await _ctx.DisposeComponentsAsync();

        Assert.Equal(["Book"], _sender.Stored(WishlistSender.Anna));
    }

    [Fact]
    public void ACompletedSync_ShowsTheWishlistItPulledIn()
    {
        var cut = Render();

        _sender.SetFromElsewhere(WishlistSender.Beat, ["Map", "Knife"]);
        _signals.RaiseSyncCompleted();

        cut.WaitForAssertion(() => Assert.Equal("Map\nKnife", Text(cut, 1)));
    }

    [Fact]
    public void ACompletedSync_KeepsTextStillBeingTyped()
    {
        var cut = Render();

        Type(cut, 0, "Book");
        _sender.SetFromElsewhere(WishlistSender.Anna, ["Older"]);
        _signals.RaiseSyncCompleted();

        cut.WaitForAssertion(() => Assert.Equal(["Book"], _sender.Stored(WishlistSender.Anna)), PastThePause);
        Assert.Equal("Book", Text(cut, 0));
    }

    [Fact]
    public void LeavingAChangedField_OffersUndo_WhichRestoresAndSavesTheEarlierText()
    {
        _sender.SetFromElsewhere(WishlistSender.Anna, ["Scarf"]);
        var snackbars = _ctx.Render<MudBlazor.MudSnackbarProvider>();
        var cut = Render();

        Type(cut, 0, "Scarf\nBook");
        Blur(cut, 0);
        cut.WaitForAssertion(() => Assert.Equal(["Scarf", "Book"], _sender.Stored(WishlistSender.Anna)));
        snackbars.WaitForAssertion(() => Assert.Contains("ChangeSaved", snackbars.Markup));

        ClickUndo(snackbars);

        cut.WaitForAssertion(() => Assert.Equal(["Scarf"], _sender.Stored(WishlistSender.Anna)));
        cut.WaitForAssertion(() => Assert.Equal("Scarf", Text(cut, 0)));
    }

    [Fact]
    public void LeavingAFieldWithoutChangingIt_OffersNoUndo()
    {
        var snackbars = _ctx.Render<MudBlazor.MudSnackbarProvider>();
        var cut = Render();

        Blur(cut, 0);

        Assert.DoesNotContain("ChangeSaved", snackbars.Markup);
    }

    private IRenderedComponent<EventWishlists> Render()
    {
        var cut = _ctx.Render<EventWishlists>(p => p
            .Add(c => c.GroupId, WishlistSender.GroupId)
            .Add(c => c.EventId, WishlistSender.EventId));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("textarea").Count));
        return cut;
    }

    // Input/blur inside the renderer's dispatcher: each keystroke re-renders the page, and an element found
    // outside it can carry a retired handler id by the time the event is raised.
    private static void Type(IRenderedComponent<EventWishlists> cut, int index, string text) =>
        cut.InvokeAsync(() => cut.FindAll("textarea")[index].Input(text)).GetAwaiter().GetResult();

    private static void Blur(IRenderedComponent<EventWishlists> cut, int index) =>
        cut.InvokeAsync(() => cut.FindAll("textarea")[index].Blur()).GetAwaiter().GetResult();

    private static string Text(IRenderedComponent<EventWishlists> cut, int index) =>
        cut.FindAll("textarea")[index].GetAttribute("value") ?? cut.FindAll("textarea")[index].TextContent;

    // The undo offer is a snackbar action (UndoLastEdit); the provider renders it outside the page.
    private static void ClickUndo(IRenderedComponent<MudBlazor.MudSnackbarProvider> snackbars)
    {
        snackbars.WaitForAssertion(() => Assert.Contains(snackbars.FindAll("button"), b => b.TextContent.Trim() == "Undo"));
        snackbars.InvokeAsync(() => snackbars.FindAll("button").First(b => b.TextContent.Trim() == "Undo").Click()).GetAwaiter().GetResult();
    }

    private static string? Status(IRenderedComponent<EventWishlists> cut, int index) =>
        cut.FindAll(".mud-card")[index].QuerySelector("[role=status]")?.TextContent.Trim();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();
}

/// <summary>An organizer's two-member group with one event, whose wishlists the page reads and writes.</summary>
internal sealed class WishlistSender : ISender
{
    public static readonly Guid GroupId = Guid.NewGuid();
    public static readonly Guid EventId = Guid.NewGuid();
    public static readonly Guid Anna = Guid.NewGuid();
    public static readonly Guid Beat = Guid.NewGuid();

    private readonly Dictionary<Guid, IReadOnlyList<string>> _wishlists = new() { [Anna] = [], [Beat] = [] };
    private readonly Lock _gate = new();

    public IReadOnlyList<string> Stored(Guid memberId)
    {
        lock (_gate)
        {
            return _wishlists[memberId];
        }
    }

    // Another device's edit that a sync pulled into the local store.
    public void SetFromElsewhere(Guid memberId, IReadOnlyList<string> items)
    {
        lock (_gate)
        {
            _wishlists[memberId] = items;
        }
    }

    public Task Send(ICommand command, CancellationToken cancellationToken = default)
    {
        if (command is SetMemberWishlistCommand set)
        {
            SetFromElsewhere(set.MemberId, set.Items);
        }

        return Task.CompletedTask;
    }

    public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TResult> Send<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        object result = query switch
        {
            GetEventQuery => Event(),
            GetGroupQuery => new GroupDto(GroupId, "Familie", [new MemberDto(Anna, "Anna"), new MemberDto(Beat, "Beat")], [], []),
            _ => throw new NotSupportedException(query.GetType().Name),
        };
        return Task.FromResult((TResult)result);
    }

    private EventDto Event()
    {
        lock (_gate)
        {
            return new EventDto(EventId, GroupId, "Christmas", null, null, false, [], false,
                [new MemberWishlistDto(Anna, "Anna", _wishlists[Anna]), new MemberWishlistDto(Beat, "Beat", _wishlists[Beat])]);
        }
    }
}

/// <summary>An in-memory <see cref="IKeyValueStore"/> so the back navigation can record recency.</summary>
file sealed class WishlistStubKeyValueStore : IKeyValueStore
{
    private readonly Dictionary<string, string> _values = [];

    public ValueTask<string?> GetAsync(StorageScope scope, string key, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_values.TryGetValue(key, out var value) ? value : null);

    public ValueTask<bool> SetAsync(StorageScope scope, string key, string value, CancellationToken cancellationToken = default)
    {
        _values[key] = value;
        return ValueTask.FromResult(true);
    }

    public ValueTask RemoveAsync(StorageScope scope, string key, CancellationToken cancellationToken = default)
    {
        _values.Remove(key);
        return ValueTask.CompletedTask;
    }
}
