using Bunit;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Ui.Shared;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Component tests for the getting-started checklist (FEAT-019, OB-8): it guides a new group from empty to
/// a shared draw, ticks steps off as they are done, disappears once the group is set up, and can be skipped
/// (a device-local, per-group choice). With the pass-through localizer, resource keys echo back as text, so
/// the assertions look for keys (e.g. <c>SetupTitle</c>, <c>GoToMembers</c>).
/// </summary>
public sealed class GettingStartedChecklistTests : IDisposable
{
    private readonly Bunit.TestContext _ctx = new();
    private readonly FakeKeyValueStore _store = new();

    public GettingStartedChecklistTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        _ctx.Services.AddSingleton<IKeyValueStore>(_store);
    }

    private static GroupDto Group(int members = 0, int events = 0, bool drawn = false) => new(
        Guid.NewGuid(),
        "Test",
        [.. Enumerable.Range(0, members).Select(i => new MemberDto(Guid.NewGuid(), $"M{i}"))],
        [],
        [.. Enumerable.Range(0, events).Select(i =>
            new EventSummaryDto(Guid.NewGuid(), $"E{i}", HasCurrentDraw: drawn, DrawInvalidatedByMembershipChange: false))]);

    private IRenderedComponent<GettingStartedChecklist> Render(GroupDto group) =>
        _ctx.RenderComponent<GettingStartedChecklist>(p => p.Add(c => c.Group, group));

    [Fact]
    public void Shows_OnAFreshGroup_WithNameDoneAndFourSteps()
    {
        var cut = Render(Group());

        Assert.Contains("SetupTitle", cut.Markup);
        // "Name your group" + add members + add an occasion + draw names.
        Assert.Equal(4, cut.FindAll(".ct-setup-step").Count);
    }

    [Fact]
    public void Hides_WhenTheGroupIsFullySetUp()
    {
        // Three members, an occasion, and a draw = every step done → the card removes itself.
        var cut = Render(Group(members: 3, events: 1, drawn: true));

        Assert.DoesNotContain("SetupTitle", cut.Markup);
    }

    [Fact]
    public void MembersStep_IsNotComplete_WithOnlyTwoMembers()
    {
        // Two members cannot produce a valid draw (the no-reciprocal rule), so the step must not read done
        // at two — otherwise the checklist would tick "done" while the draw still asks for more people.
        var cut = Render(Group(members: 2, events: 1));

        Assert.Contains("SetupTitle", cut.Markup); // draw not done yet, so still shown
        var membersItem = cut.FindAll(".ct-setup-step").Single(li => li.TextContent.Contains("GoToMembers"));
        Assert.DoesNotContain("line-through", membersItem.InnerHtml);
    }

    [Fact]
    public void OpenSteps_AreButtons_DoneAndLockedStepsAreNot()
    {
        // UX review U8: an open step must read (and announce) as an action; done/locked ones must not.
        var cut = Render(Group());

        var steps = cut.FindAll(".ct-setup-step");
        Assert.Null(steps.Single(s => s.TextContent.Contains("SetupStepName")).GetAttribute("role"));   // done
        Assert.Equal("button", steps.Single(s => s.TextContent.Contains("GoToMembers")).GetAttribute("role"));
        Assert.Equal("button", steps.Single(s => s.TextContent.Contains("SetupStepEvent")).GetAttribute("role"));
        Assert.Null(steps.Single(s => s.TextContent.Contains("SetupStepDraw")).GetAttribute("role"));    // locked
    }

    [Fact]
    public void MembersStep_IsComplete_AtThreeMembers()
    {
        var cut = Render(Group(members: 3));

        var membersItem = cut.FindAll(".ct-setup-step").Single(li => li.TextContent.Contains("GoToMembers"));
        Assert.Contains("line-through", membersItem.InnerHtml);
    }

    [Fact]
    public async Task Skip_PersistsDismissal_AndAFreshRenderStaysHidden()
    {
        var group = Group();
        var cut = Render(group);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "SetupSkip").Click();

        // The skip is persisted for this group (device-local).
        var flag = await _store.GetAsync(StorageScope.Durable, $"secretsanta.setup.dismissed.{group.Id}");
        Assert.Equal("1", flag);

        // A fresh render of the same group reads the flag and shows nothing.
        var again = Render(group);
        Assert.DoesNotContain("SetupTitle", again.Markup);
    }

    public void Dispose() => _ctx.Dispose();
}
