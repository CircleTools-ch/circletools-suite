using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.SecretSanta.LocalStore.Online;

/// <summary>Where a group lives (ADR-0086).</summary>
public enum GroupPlace
{
    /// <summary>Only on this device: no server copy, nobody joins.</summary>
    Device = 0,

    /// <summary>One server copy in the Secret-Santa group store.</summary>
    Online = 1,
}

/// <summary>How this device proves its role in an online group (ADR-0086).</summary>
public enum AccessPrincipal
{
    /// <summary>The signed-in account: the group belongs to whoever is signed in as <see cref="OnlineAccess.AccountId"/>.</summary>
    Account = 1,

    /// <summary>The organizer link, held in <see cref="OnlineAccess.Secret"/>.</summary>
    OrganizerLink = 2,

    /// <summary>A device key from a join without an account, held in <see cref="OnlineAccess.Secret"/>.</summary>
    DeviceKey = 3,
}

/// <summary>The permission names the server reports (ADR-0086); pages decide from these, never from a role name.</summary>
public static class OnlinePermissions
{
    /// <summary>Read the whole document.</summary>
    public const string ReadDocument = "readDocument";

    /// <summary>Push the document.</summary>
    public const string WriteDocument = "writeDocument";

    /// <summary>Add, invite and release participants.</summary>
    public const string ManageParticipants = "manageParticipants";

    /// <summary>Manage the join and organizer links.</summary>
    public const string ManageLinks = "manageLinks";

    /// <summary>Hand over, (later) appoint admins.</summary>
    public const string ManageRoles = "manageRoles";

    /// <summary>Delete the online copy.</summary>
    public const string DeleteGroup = "deleteGroup";

    /// <summary>Read one's own view.</summary>
    public const string ReadOwnView = "readOwnView";

    /// <summary>Write one's own entry.</summary>
    public const string WriteOwnEntry = "writeOwnEntry";

    /// <summary>Leave.</summary>
    public const string Leave = "leave";
}

/// <summary>What this device may do in an online group, and how it proves it (ADR-0086).</summary>
/// <param name="Principal">How this device proves its role.</param>
/// <param name="Role">The role as the server named it (<c>owner</c>, <c>participant</c>; more later).</param>
/// <param name="Permissions">The permissions the server reported.</param>
/// <param name="AccountId">For an account principal, the account the group belongs to on this device.</param>
/// <param name="MemberId">The roster member this device is, if any.</param>
/// <param name="Secret">The organizer token or device key, for those principals.</param>
/// <param name="CreatorKey">The creator key minted when this device made the group online without an account.</param>
/// <param name="JoinToken">The join link's token, while it is on and this device may manage links.</param>
public sealed record OnlineAccess(
    AccessPrincipal Principal,
    string Role,
    IReadOnlyList<string> Permissions,
    Guid? AccountId = null,
    Guid? MemberId = null,
    string? Secret = null,
    string? CreatorKey = null,
    string? JoinToken = null)
{
    /// <summary>Whether the server granted a permission.</summary>
    /// <param name="permission">One of <see cref="OnlinePermissions"/>.</param>
    /// <returns><see langword="true"/> when granted.</returns>
    public bool Can(string permission) => Permissions.Contains(permission, StringComparer.Ordinal);

    /// <summary>Gets the credentials to send besides the bearer.</summary>
    public OnlineCredentials Credentials => Principal switch
    {
        AccessPrincipal.OrganizerLink => new OnlineCredentials(OrganizerToken: Secret),
        AccessPrincipal.DeviceKey => new OnlineCredentials(DeviceKey: Secret),
        _ => OnlineCredentials.None,
    };

    /// <summary>Whether the group belongs on this device for <paramref name="signedInAccount"/>.</summary>
    /// <param name="signedInAccount">The signed-in account, if any.</param>
    /// <returns><see langword="true"/> for link and key principals, and for an account principal of that account.</returns>
    public bool IsVisibleTo(Guid? signedInAccount) => Principal != AccessPrincipal.Account || AccountId == signedInAccount;
}

/// <summary>
/// One group as this device holds it (ADR-0086): its place, this device's access, and either the whole document
/// (organizer) or the participant view.
/// </summary>
/// <param name="Id">The group.</param>
/// <param name="Place">Where it lives.</param>
/// <param name="Document">The whole document, for a group this device organizes (or a device group).</param>
/// <param name="View">The participant view, for a group this device takes part in.</param>
/// <param name="ServerVersion">The server version the local copy is based on (0 before it was online).</param>
/// <param name="Dirty">Whether the document holds an edit not yet pushed.</param>
/// <param name="Access">This device's access, for an online group.</param>
/// <param name="PendingEntry">An own entry written here and not yet accepted by the server.</param>
/// <param name="SelfMaintained">Members who keep their own entry (their fields are read-only for the organizer).</param>
/// <param name="Roles">Who has a role in the group, as the organizer last read it.</param>
public sealed record GroupRecord(
    Guid Id,
    GroupPlace Place,
    GroupState? Document,
    ParticipantView? View,
    long ServerVersion,
    bool Dirty,
    OnlineAccess? Access = null,
    OwnEntryRequest? PendingEntry = null,
    IReadOnlyList<Guid>? SelfMaintained = null,
    IReadOnlyList<GroupRoleView>? Roles = null)
{
    /// <summary>Whether this device may change the document.</summary>
    public bool CanEditDocument => Document is not null && (Place == GroupPlace.Device || Access?.Can(OnlinePermissions.WriteDocument) == true);

    /// <summary>Whether the group belongs on this device for <paramref name="signedInAccount"/>.</summary>
    /// <param name="signedInAccount">The signed-in account, if any.</param>
    /// <returns><see langword="true"/> when it should be shown.</returns>
    public bool IsVisibleTo(Guid? signedInAccount) => Place == GroupPlace.Device || Access?.IsVisibleTo(signedInAccount) != false;
}

/// <summary>An online group removed on this device, whose server side still has to be told (ADR-0086).</summary>
/// <param name="GroupId">The group.</param>
/// <param name="Access">The access it was removed with.</param>
/// <param name="Leave">Whether to leave (participant) rather than delete the online copy (organizer).</param>
public sealed record PendingRemoval(Guid GroupId, OnlineAccess Access, bool Leave);

/// <summary>Everything this device holds about groups, under one storage key (ADR-0086).</summary>
/// <param name="SchemaVersion">The local schema version.</param>
/// <param name="Records">The groups.</param>
/// <param name="Removals">Server-side removals still to send.</param>
/// <param name="Invitations">Invitations waiting for the signed-in account's answer, as last discovered.</param>
public sealed record GroupRecordsDocument(
    int SchemaVersion,
    IReadOnlyList<GroupRecord> Records,
    IReadOnlyList<PendingRemoval> Removals,
    IReadOnlyList<MyGroup> Invitations)
{
    /// <summary>The current local schema (4: the ADR-0086 model; older keys are left behind, decision D2).</summary>
    public const int CurrentSchemaVersion = 4;

    /// <summary>Gets an empty document.</summary>
    public static GroupRecordsDocument Empty { get; } = new(CurrentSchemaVersion, [], [], []);
}
