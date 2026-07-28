namespace AppSuite.Ui.Abstractions;

/// <summary>
/// A WebAuthn ceremony failed for a reason that is not the user changing their mind.
/// </summary>
/// <remarks>
/// The authenticator refused, the options were malformed, or the platform has no passkey support at
/// all. Cancelling is <em>not</em> one of these — that returns <see langword="null"/> (FR-011).
/// <para>
/// It exists so that pages can react to a broken ceremony without catching the host's own exception
/// type: a page in <c>Main.Ui</c> that caught <c>JSException</c> would quietly stop being portable
/// to a MAUI head, which is the one thing these abstractions are for.
/// </para>
/// </remarks>
public sealed class PasskeyCeremonyException : Exception
{
    /// <summary>Initialises the exception.</summary>
    public PasskeyCeremonyException()
        : base("The passkey ceremony failed.")
    {
    }

    /// <summary>Initialises the exception with a message.</summary>
    /// <param name="message">What went wrong.</param>
    public PasskeyCeremonyException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises the exception with a message and the failure underneath it.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The platform failure.</param>
    public PasskeyCeremonyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
