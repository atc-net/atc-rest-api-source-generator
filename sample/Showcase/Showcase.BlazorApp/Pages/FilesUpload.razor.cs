namespace Showcase.BlazorApp.Pages;

public partial class FilesUpload
{
    [Inject]
    private GatewayService Gateway { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    private bool isLoading;

    // Single file
    private IBrowserFile? singleFile;

    // Multiple files - store as tuples with pre-read data to avoid stale IBrowserFile references
    private IReadOnlyList<IBrowserFile>? multipleFiles;
    private List<(MemoryStream Stream, string Name, string ContentType, long Size)>? multipleFilesData;

    // File with metadata
    private IBrowserFile? metadataFile;
    private string itemName = string.Empty;
    private string itemsText = string.Empty;

    // Batch files with metadata
    private IReadOnlyList<IBrowserFile>? batchFiles;
    private string batchItemName = string.Empty;
    private string batchItemsText = string.Empty;

    private async Task OnMultipleFilesSelected()
    {
        if (multipleFiles == null || multipleFiles.Count == 0)
        {
            return;
        }

        // Dispose previous files if any
        if (multipleFilesData != null)
        {
            foreach (var (stream, _, _, _) in multipleFilesData)
            {
                await stream.DisposeAsync();
            }
        }

        // Read all files into memory immediately to avoid stale IBrowserFile references
        multipleFilesData = new List<(MemoryStream, string, string, long)>();
        foreach (var file in multipleFiles)
        {
            await using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;
            multipleFilesData.Add((ms, file.Name, file.ContentType, file.Size));
        }

        StateHasChanged();
    }

    private async Task UploadSingleFileAsync()
    {
        if (singleFile == null)
        {
            return;
        }

        isLoading = true;
        try
        {
            await using var stream = singleFile.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;

            await Gateway.UploadSingleFileAsync(ms, singleFile.Name, singleFile.ContentType);
            Snackbar.Add($"Uploaded: {singleFile.Name}", Severity.Success);
            singleFile = null;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task UploadMultipleFilesAsync()
    {
        if (multipleFilesData == null || multipleFilesData.Count == 0)
        {
            return;
        }

        isLoading = true;
        try
        {
            // Reset stream positions before upload
            var files = new List<(Stream, string, string)>();
            foreach (var (stream, name, contentType, _) in multipleFilesData)
            {
                stream.Position = 0;
                files.Add((stream, name, contentType));
            }

            await Gateway.UploadMultipleFilesAsync(files);

            // Dispose streams after upload
            foreach (var (stream, _, _, _) in multipleFilesData)
            {
                await stream.DisposeAsync();
            }

            Snackbar.Add($"Uploaded {multipleFilesData.Count} files", Severity.Success);
            multipleFilesData = null;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task UploadFileWithMetadataAsync()
    {
        if (metadataFile == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(itemName))
        {
            Snackbar.Add("Item Name is required", Severity.Warning);
            return;
        }

        isLoading = true;
        try
        {
            await using var stream = metadataFile.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;

            var items = itemsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            await Gateway.UploadSingleObjectWithFileAsync(ms, metadataFile.Name, metadataFile.ContentType, itemName, items);
            Snackbar.Add($"Uploaded: {metadataFile.Name} with metadata", Severity.Success);
            metadataFile = null;
            itemName = string.Empty;
            itemsText = string.Empty;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task UploadBatchWithMetadataAsync()
    {
        if (batchFiles == null || batchFiles.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(batchItemName))
        {
            Snackbar.Add("Item Name is required", Severity.Warning);
            return;
        }

        isLoading = true;
        try
        {
            var files = new List<(Stream, string, string)>();
            var memoryStreams = new List<MemoryStream>();

            foreach (var file in batchFiles)
            {
                await using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
                var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ms.Position = 0;
                memoryStreams.Add(ms);
                files.Add((ms, file.Name, file.ContentType));
            }

            var items = batchItemsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            await Gateway.UploadSingleObjectWithMultipleFilesAsync(files, batchItemName, items);

            foreach (var ms in memoryStreams)
            {
                await ms.DisposeAsync();
            }

            Snackbar.Add($"Uploaded {batchFiles.Count} files with metadata", Severity.Success);
            batchFiles = null;
            batchItemName = string.Empty;
            batchItemsText = string.Empty;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoading = false;
        }
    }
}