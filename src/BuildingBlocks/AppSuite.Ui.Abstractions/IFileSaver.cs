namespace AppSuite.Ui.Abstractions;

/// <summary>
/// Hands a generated file to the user through whatever the platform calls "save".
/// </summary>
/// <remarks>
/// A browser triggers a download; a device head would offer a share sheet or write to the documents
/// folder. Only the data export needs this today (FR-014), and the whole point of that feature is
/// that the copy ends up somewhere the user controls — so it cannot be faked with a page that just
/// displays the content.
/// </remarks>
public interface IFileSaver
{
    /// <summary>Saves text content as a file.</summary>
    /// <param name="fileName">The suggested file name, including its extension.</param>
    /// <param name="contentType">The MIME type, for example <c>application/json</c>.</param>
    /// <param name="content">The file's contents.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A task that completes once the platform has taken the file.</returns>
    ValueTask SaveAsync(
        string fileName,
        string contentType,
        string content,
        CancellationToken cancellationToken = default);
}
