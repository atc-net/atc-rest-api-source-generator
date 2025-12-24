namespace Showcase.BlazorApp.Pages;

public partial class Files
{
    private readonly (string Id, string Name, string Icon)[] sampleFiles =
    [
        ("1", "sample-readme.txt", Icons.Material.Filled.Description),
        ("2", "config-example.json", Icons.Material.Filled.Code),
        ("3", "data-sample.xml", Icons.Material.Filled.DataObject),
        ("4", "sample-image.png", Icons.Material.Filled.Image),
        ("5", "checkerboard.png", Icons.Material.Filled.Image),
    ];

    [Inject]
    private GatewayService Gateway { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private IJSRuntime JavaScriptRuntime { get; set; } = null!;

    private string fileId = "1";
    private string? selectedFileId;
    private bool isLoading;
    private byte[]? previewContent;
    private string? previewContentType;
    private string? previewFileName;
    private string? previewText;
    private string? previewDataUrl;

    private async Task DownloadFileAsync()
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            Snackbar.Add("Please enter a file ID", Severity.Warning);
            return;
        }

        isLoading = true;
        try
        {
            var result = await Gateway.GetFileByIdAsync(fileId);
            if (result == null)
            {
                Snackbar.Add("File not found", Severity.Warning);
                return;
            }

            var (content, contentType, fileName) = result.Value;
            await DownloadFileToUserAsync(content, fileName ?? $"file-{fileId}", contentType);
            Snackbar.Add($"Downloaded: {fileName ?? fileId}", Severity.Success);
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

    private async Task PreviewFileAsync()
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            Snackbar.Add("Please enter a file ID", Severity.Warning);
            return;
        }

        isLoading = true;
        try
        {
            var result = await Gateway.GetFileByIdAsync(fileId);
            if (result == null)
            {
                Snackbar.Add("File not found", Severity.Warning);
                return;
            }

            var (content, contentType, fileName) = result.Value;
            SetPreview(content, contentType, fileName ?? $"file-{fileId}");
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

    private async Task QuickDownloadAsync(string id)
    {
        fileId = id;
        await DownloadFileAsync();
    }

    private async Task QuickPreviewAsync(string id)
    {
        fileId = id;
        selectedFileId = id;
        await PreviewFileAsync();
    }

    private void SetPreview(
        byte[] content,
        string contentType,
        string fileName)
    {
        // Clear previous preview data
        previewText = null;
        previewDataUrl = null;

        previewContent = content;
        previewContentType = contentType;
        previewFileName = fileName;

        if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("application/xml", StringComparison.OrdinalIgnoreCase))
        {
            previewText = System.Text.Encoding.UTF8.GetString(content);
        }
        else if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            var base64 = Convert.ToBase64String(content);
            previewDataUrl = $"data:{contentType};base64,{base64}";
        }
    }

    private void ClearPreview()
    {
        previewContent = null;
        previewContentType = null;
        previewFileName = null;
        previewText = null;
        previewDataUrl = null;
        selectedFileId = null;
    }

    private async Task DownloadPreviewedFileAsync()
    {
        if (previewContent == null || previewFileName == null || previewContentType == null)
        {
            return;
        }

        await DownloadFileToUserAsync(previewContent, previewFileName, previewContentType);
        Snackbar.Add($"Downloaded: {previewFileName}", Severity.Success);
    }

    private static string FormatFileSize(int bytes)
        => bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB",
        };

    private async Task DownloadFileToUserAsync(
        byte[] content,
        string fileName,
        string contentType)
    {
        var base64 = Convert.ToBase64String(content);
        var dataUrl = $"data:{contentType};base64,{base64}";

        await JavaScriptRuntime.InvokeVoidAsync("eval", $@"
            var a = document.createElement('a');
            a.href = '{dataUrl}';
            a.download = '{fileName}';
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
        ");
    }
}