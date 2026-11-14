// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.Ui.Shared;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Abstractions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// The back link above an event's section pages names the event; a rename saved on the page below shows
/// there at once, not only after navigating away (BTP-9).
/// </summary>
public sealed class EventBackNavTests : IAsyncDisposable
{
    private readonly Bunit.BunitContext _ctx = new();
    private readonly NamedEventSender _sender = new();
    private readonly FakeSyncSignals _signals = new();

    public EventBackNavTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        _ctx.Services.AddSingleton<ISender>(_sender);
        _ctx.Services.AddSingleton<ISyncSignals>(_signals);
        _ctx.Services.AddSingleton<IKeyValueStore>(new InMemoryKeyValueStore());
        _ctx.Services.AddSingleton<RecentAccessService>();
    }

    [Fact]
    public void ARenameSavedOnThePage_ShowsInTheBackLinkAtOnce()
    {
        var cut = _ctx.Render<EventBackNav>(p => p
            .Add(c => c.GroupId, NamedEventSender.GroupId)
            .Add(c => c.EventId, NamedEventSender.EventId));
        cut.WaitForAssertion(() => Assert.Contains("Christmas 2026", cut.Markup, StringComparison.Ordinal));

        _sender.Name = "Family Christmas 2026";
        _signals.RaiseLocalChanged();

        cut.WaitForAssertion(() => Assert.Contains("Family Christmas 2026", cut.Markup, StringComparison.Ordinal));
    }

    public async ValueTask DisposeAsync() => await _ctx.DisposeAsync();

    private sealed class NamedEventSender : ISender
    {
        public static readonly Guid GroupId = Guid.CreateVersion7();
        public static readonly Guid EventId = Guid.CreateVersion7();

        public volatile string Name = "Christmas 2026";

        public Task Send(ICommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TResult> Send<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) =>
            query is GetEventQuery
                ? Task.FromResult((TResult)(object)new EventDto(EventId, GroupId, Name, null, null, false, [], false, []))
                : throw new NotSupportedException(query.GetType().Name);
    }

    private sealed class InMemoryKeyValueStore : IKeyValueStore
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
}
