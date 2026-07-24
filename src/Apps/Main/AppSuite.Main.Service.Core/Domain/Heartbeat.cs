using AppSuite.SharedKernel;

namespace AppSuite.Main.Service.Core.Domain;

/// <summary>
/// THROWAWAY skeleton sample aggregate — NOT part of the real Main domain. It exists
/// only to exercise the end-to-end slice (API → application → domain → persistence).
/// It will be removed once the first real bounded context is modeled.
/// </summary>
public sealed class Heartbeat : AggregateRoot
{
    // EF Core materialization constructor.
    private Heartbeat()
    {
        Note = string.Empty;
    }

    private Heartbeat(Guid id, DateTimeOffset createdAt, string note)
    {
        Id = id;
        CreatedAt = createdAt;
        Note = note;
    }

    /// <summary>Gets the instant the heartbeat was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the short free-text note attached to the heartbeat.</summary>
    public string Note { get; private set; }

    /// <summary>Creates a new <see cref="Heartbeat"/> with a time-ordered identity.</summary>
    /// <param name="note">A short, non-empty note.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The created heartbeat.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="note"/> is null or whitespace.</exception>
    public static Heartbeat Create(string note, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("Note must not be empty.", nameof(note));
        }

        return new Heartbeat(GuidV7.New(), now, note.Trim());
    }
}
