namespace Showcase.BlazorApp.Services;

/// <summary>
/// Gateway service - Files operations using generated endpoints.
/// Binary file endpoints now use BinaryEndpointResponse from Atc.Rest.Client
/// which provides typed access to Content (byte[]), ContentType, and FileName.
/// </summary>
public sealed partial class GatewayService
{
    /// <summary>
    /// Get file by ID and return as byte array with content type.
    /// The generated result gives the file as <c>OkContent</c> (bytes, content type, file name) and the errors as typed
    /// accessors, like any other operation.
    /// </summary>
    public async Task<(byte[] Content, string ContentType, string? FileName)?> GetFileByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var parameters = new GetFileByIdParameters(Id: id);
        var result = await getFileByIdEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsOk)
        {
            return null;
        }

        var file = result.OkContent;
        return (file.Content, file.ContentType ?? "application/octet-stream", file.FileName);
    }

    /// <summary>
    /// Upload a single file using form data.
    /// </summary>
    public async Task UploadSingleFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var fileContent = new StreamFileContent(fileStream, fileName, contentType);
        var parameters = new UploadSingleFileAsFormDataParameters(File: fileContent);

        var result = await uploadSingleFileAsFormDataEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsOk)
        {
            throw new HttpRequestException($"Failed to upload file: {result.StatusCode}");
        }
    }

    /// <summary>
    /// Upload multiple files using form data.
    /// </summary>
    public async Task UploadMultipleFilesAsync(
        IEnumerable<(Stream Stream, string FileName, string ContentType)> files,
        CancellationToken cancellationToken = default)
    {
        var fileContents = files
            .Select(f => (Atc.Rest.Client.IFileContent)new StreamFileContent(f.Stream, f.FileName, f.ContentType))
            .ToArray();
        var parameters = new UploadMultiFilesAsFormDataParameters(File: fileContents);

        var result = await uploadMultiFilesAsFormDataEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsOk)
        {
            throw new HttpRequestException($"Failed to upload files: {result.StatusCode}");
        }
    }

    /// <summary>
    /// Upload a single file with metadata (item name and items).
    /// </summary>
    public async Task UploadSingleObjectWithFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string itemName,
        string[] items,
        string? author = null,
        CancellationToken cancellationToken = default)
    {
        var fileContent = new StreamFileContent(fileStream, fileName, contentType);

        // The metadata goes as a URL-encoded part (author=Ada), as the form's encoding names
        var metadata = string.IsNullOrWhiteSpace(author) ? null : new FileMetadata(Author: author, Confidential: null);
        var request = new FileAsFormDataRequest(ItemName: itemName, File: fileContent, Items: [..items], Metadata: metadata);
        var parameters = new UploadSingleObjectWithFileAsFormDataParameters(Request: request);

        var result = await uploadSingleObjectWithFileAsFormDataEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsOk)
        {
            throw new HttpRequestException($"Failed to upload file with metadata: {result.StatusCode}");
        }
    }

    /// <summary>
    /// Upload multiple files as the files of a FilesAsFormDataRequest model (its one property).
    /// </summary>
    public async Task UploadSingleObjectWithMultipleFilesAsync(
        IEnumerable<(Stream Stream, string FileName, string ContentType)> files,
        CancellationToken cancellationToken = default)
    {
        var request = new FilesAsFormDataRequest([..files.Select(f => (Atc.Rest.Client.IFileContent)new StreamFileContent(f.Stream, f.FileName, f.ContentType))]);
        var parameters = new UploadSingleObjectWithFilesAsFormDataParameters(Request: request);

        var result = await uploadSingleObjectWithFilesAsFormDataEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsOk)
        {
            throw new HttpRequestException($"Failed to upload files with metadata: {result.StatusCode}");
        }
    }
}