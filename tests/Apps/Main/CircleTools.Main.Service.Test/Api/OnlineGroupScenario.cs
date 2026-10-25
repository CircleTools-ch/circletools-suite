using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>Drives the Secret-Santa group store's endpoints (ADR-0086) the way the apps will.</summary>
internal static class OnlineGroupScenario
{
    public const string Base = "/api/v1/secretsanta";
    public const string OrganizerTokenHeader = "X-Organizer-Token";
    public const string DeviceKeyHeader = "X-Device-Key";

    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A drawn group of four with one event, built through the real domain.</summary>
    public static (GroupState Document, Guid EventId, IReadOnlyDictionary<string, Guid> Members) DrawnGroup(string name = "Family")
    {
        var group = Group.Create(name);
        var members = new Dictionary<string, Guid>();
        foreach (var member in new[] { "Anna", "Ben", "Cleo", "Dan" })
        {
            members[member] = group.AddMember(member);
        }

        var eventId = group.AddEvent("Christmas 2026");
        Assert.True(group.RunDraw(eventId, new DrawGenerator(new SystemRandomSource()), Now).IsSuccess);
        return (group.ToState(), eventId, members);
    }

    public static JsonElement Element(GroupState document)
    {
        using var parsed = JsonDocument.Parse(GroupDocument.Serialize(document));
        return parsed.RootElement.Clone();
    }

    public static GroupState Read(JsonElement element)
    {
        Assert.True(GroupDocument.TryDeserialize(element.GetRawText(), out var document));
        return document!;
    }

    public static Guid RecipientOf(GroupState document, Guid eventId, Guid giver) =>
        Group.FromState(document).FindEvent(eventId)!.CurrentDraw!.Assignments.Single(a => a.GiverMemberId == giver).RecipientMemberId;

    public static string NameOf(GroupState document, Guid memberId) => Group.FromState(document).FindMember(memberId)!.Name;

    /// <summary>Makes a group online as the signed-in account, with the join link on.</summary>
    public static async Task<GroupLinks> CreateAsync(HttpClient client, GroupState document, bool joinEnabled = true, string? creatorKey = null)
    {
        var response = await client.PostAsJsonAsync($"{Base}/groups", new CreateOnlineGroupRequest(1, Element(document), creatorKey, joinEnabled));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GroupLinks>())!;
    }

    public static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object? body = null, string? organizerToken = null, string? deviceKey = null)
    {
        var request = new HttpRequestMessage(method, $"{Base}{path}");
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        if (organizerToken is not null)
        {
            request.Headers.Add(OrganizerTokenHeader, organizerToken);
        }

        if (deviceKey is not null)
        {
            request.Headers.Add(DeviceKeyHeader, deviceKey);
        }

        return client.SendAsync(request);
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.StatusCode}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
