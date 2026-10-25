using System.Text;
using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.SecretSanta.Service.Core.Groups;

/// <summary>
/// The bounds on what an online group may hold and how long an unused one is kept (NFR-009, NFR-010). Anyone with a
/// link can write without an account, so a document is capped in size and roster length.
/// </summary>
/// <remarks>
/// Bound to the <c>SharedGroups</c> configuration section, the name the retired shared-group store used: deployments
/// already set it (and its <c>Security</c> key), so the keys keep working.
/// </remarks>
public sealed class GroupLimits
{
    /// <summary>The configuration section these limits bind to.</summary>
    public const string SectionName = "SharedGroups";

    /// <summary>Gets or sets the largest stored document, in bytes (UTF-8). Default 256 KiB.</summary>
    public int MaxBodyBytes { get; set; } = 256 * 1024;

    /// <summary>Gets or sets the most members a group may have. Default 200.</summary>
    public int MaxMembers { get; set; } = 200;

    /// <summary>Gets or sets how long a group no account owns is kept without any use. Default 365 days.</summary>
    public int RetentionDays { get; set; } = 365;

    /// <summary>Whether a document stays within the bounds.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when it may be stored.</returns>
    public bool AreMet(GroupState document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Members.Count <= MaxMembers
            && Encoding.UTF8.GetByteCount(GroupDocument.Serialize(document)) <= MaxBodyBytes;
    }
}
