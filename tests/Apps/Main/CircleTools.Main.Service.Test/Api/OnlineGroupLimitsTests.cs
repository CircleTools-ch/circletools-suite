using System.Net;
using System.Net.Http.Json;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Service.Core.Groups;
using CircleTools.SecretSanta.Service.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CircleTools.Main.Service.Test.Api.OnlineGroupScenario;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// The bounds on the group store (ADR-0086): a document past the size limits is refused (NFR-009), and a group no
/// account owns is deleted after a year without use, while a group in use or owned by an account is kept (NFR-010).
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class OnlineGroupLimitsTests(MailApiFactory factory)
{
    [Fact]
    public async Task ARosterPastTheLimit_IsRefusedOnCreateAndOnPush()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Owner");
        var crowd = Group.Create("Crowd");
        for (var i = 0; i <= new GroupLimits().MaxMembers; i++)
        {
            crowd.AddMember($"Member {i}");
        }

        var create = await owner.Client.PostAsJsonAsync($"{Base}/groups", new CreateOnlineGroupRequest(1, Element(crowd.ToState()), null, false));
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

        var (document, _, _) = DrawnGroup();
        await CreateAsync(owner.Client, document);
        var grown = Group.FromState(document);
        for (var i = 0; i <= new GroupLimits().MaxMembers; i++)
        {
            grown.AddMember($"Guest {i}");
        }

        var push = await SendAsync(owner.Client, HttpMethod.Put, $"/groups/{document.Id}", new PushOnlineGroupRequest(1, 1, Element(grown.ToState())));
        Assert.Equal(HttpStatusCode.BadRequest, push.StatusCode);
    }

    [Fact]
    public async Task Retention_DeletesAnUnusedGroupWithoutAnAccountOwner_KeepingOnesInUseOrOwned()
    {
        using var device = factory.CreateClient();
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Owner");
        var (abandoned, _, _) = DrawnGroup("Abandoned");
        var (inUse, _, _) = DrawnGroup("In use");
        var (owned, _, _) = DrawnGroup("Owned");
        var abandonedLinks = await CreateAsync(device, abandoned, creatorKey: "creator-1");
        var inUseLinks = await CreateAsync(device, inUse, creatorKey: "creator-2");
        await CreateAsync(owner.Client, owned);

        await AgeAsync(abandoned.Id, inUse.Id, owned.Id);

        // Someone opens the group in use: any read counts as activity.
        (await SendAsync(device, HttpMethod.Get, $"/groups/{inUse.Id}", organizerToken: inUseLinks.OrganizerToken)).EnsureSuccessStatusCode();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<GroupService>().PruneInactiveAsync();
        }

        Assert.Equal(HttpStatusCode.NotFound, (await device.GetAsync($"{Base}/join/{abandonedLinks.JoinToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await device.GetAsync($"{Base}/join/{inUseLinks.JoinToken}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.Client.GetAsync($"{Base}/groups/{owned.Id}")).StatusCode);
    }

    // Moves the groups' last use back past the retention window.
    private async Task AgeAsync(params Guid[] groupIds)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var longAgo = DateTimeOffset.UtcNow - TimeSpan.FromDays(new GroupLimits().RetentionDays + 1);
        await scope.ServiceProvider.GetRequiredService<SecretSantaDbContext>().Groups
            .Where(g => groupIds.Contains(g.GroupId))
            .ExecuteUpdateAsync(set => set.SetProperty(g => g.LastActivityAt, longAgo));
    }
}
