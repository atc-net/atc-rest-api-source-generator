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
    /// Uses BinaryEndpointResponse which provides direct access to binary content.
    /// </summary>
    public async Task<(byte[] Content, string ContentType, string? FileName)?> GetFileByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var parameters = new GetFileByIdParameters(Id: id);
        var result = await getFileByIdEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsOk || result.Content is null)
        {
            return null;
        }

        return (result.Content, result.ContentType ?? "application/octet-stream", result.FileName);
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
        var parameters = new UploadSingleFileAsFormDataParameters(File: fileStream, FileName: fileName);

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
        var fileStreams = files
            .Select(f => f.Stream)
            .ToArray();
        var parameters = new UploadMultiFilesAsFormDataParameters(File: fileStreams);

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
        CancellationToken cancellationToken = default)
    {
        var request = new FileAsFormDataRequest(itemName, fileStream, items);
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
    /// Upload multiple files with metadata (item name and items).
    /// Note: The FilesAsFormDataRequest only takes Files array.
    /// ItemName and Items metadata are not part of this request type.
    /// </summary>
    public async Task UploadSingleObjectWithMultipleFilesAsync(
        IEnumerable<(Stream Stream, string FileName, string ContentType)> files,
        string itemName,
        string[] items,
        CancellationToken cancellationToken = default)
    {
        var fileStreams = files
            .Select(f => f.Stream)
            .ToArray();
        var request = new FilesAsFormDataRequest(fileStreams);
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