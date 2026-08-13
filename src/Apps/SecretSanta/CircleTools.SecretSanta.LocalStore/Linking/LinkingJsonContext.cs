using System.Text.Json.Serialization;
using CircleTools.SecretSanta.Contracts;

namespace CircleTools.SecretSanta.LocalStore.Linking;

/// <summary>
/// Source-generated JSON for the M4 linking client (FEAT-005), camel-cased to match the API. Source
/// generation rather than reflection because the Secret-Santa web app is a trimmed Blazor WebAssembly
/// build; reflection-based serialization of these records would be trimmed away.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SendMemberLinkRequest))]
[JsonSerializable(typeof(SendMemberLinkResponse))]
[JsonSerializable(typeof(RegisterCircleLinkRequest))]
[JsonSerializable(typeof(IReadOnlyList<PendingMemberLinkResponse>))]
[JsonSerializable(typeof(AcceptMemberLinkResponse))]
[JsonSerializable(typeof(MemberLinkResolutionsResponse))]
[JsonSerializable(typeof(AckMemberLinkResolutionsRequest))]
[JsonSerializable(typeof(PublishAssignmentsRequest))]
[JsonSerializable(typeof(MyAssignmentResponse))]
[JsonSerializable(typeof(MyAssignmentsResponse))]
[JsonSerializable(typeof(UpdateMyWishlistRequest))]
[JsonSerializable(typeof(MyWishlistsResponse))]
[JsonSerializable(typeof(MyMembershipsResponse))]
public sealed partial class LinkingJsonContext : JsonSerializerContext;
