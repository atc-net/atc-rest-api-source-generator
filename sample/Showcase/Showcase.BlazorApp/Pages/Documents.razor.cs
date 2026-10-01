namespace Showcase.BlazorApp.Pages;

public partial class Documents
{
    private const long MaxFileSize = 20 * 1024 * 1024;

    [Inject]
    private GatewayService Gateway { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private IJSRuntime JavaScriptRuntime { get; set; } = null!;

    private List<Document> documents = [];
    private string status = "Choose a PDF, an Excel workbook, a PNG or a JPEG.";
    private string? previewDataUrl;
    private string? previewFileName;
    private string? previewMessage;
    private DocumentSummary? summary;

    protected override async Task OnInitializedAsync()
        => documents = await Gateway.ListDocumentsAsync();

    private async Task UploadAsync(InputFileChangeEventArgs args)
    {
        foreach (var file in args.GetMultipleFiles())
        {
            try
            {
                // Read into memory first: the browser file's stream does not outlive the event
                using var buffer = new MemoryStream();
                await using (var stream = file.OpenReadStream(MaxFileSize))
                {
                    await stream.CopyToAsync(buffer);
                }

                buffer.Position = 0;
                var contentType = string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType;
                var stored = await Gateway.UploadDocumentAsync(buffer, file.Name, contentType);
                status = $"Uploaded {stored.FileName} as {stored.ContentType} ({FormatSize(stored.Length)})";
            }
            catch (Exception ex)
            {
                status = $"Upload of {file.Name} failed: {ex.Message}";
                Snackbar.Add(status, Severity.Error);
            }
        }

        documents = await Gateway.ListDocumentsAsync();
    }

    private async Task DownloadAsync(Document document)
    {
        var file = await Gateway.GetDocumentAsync(document.Id);
        if (file is null)
        {
            Snackbar.Add("Document not found", Severity.Warning);
            return;
        }

        // The file name comes from the response's Content-Disposition (the API exposes the header to the browser)
        await SaveAsync(file.Content, file.FileName ?? document.FileName, file.ContentType ?? document.ContentType);
    }

    private async Task PreviewAsync(Document document)
    {
        previewDataUrl = null;
        previewMessage = null;
        var file = await Gateway.GetDocumentAsync(document.Id);
        if (file is null)
        {
            previewMessage = "Document not found";
            return;
        }

        previewFileName = file.FileName;
        if (file.ContentType?.StartsWith("image/", StringComparison.Ordinal) == true)
        {
            previewDataUrl = $"data:{file.ContentType};base64,{Convert.ToBase64String(file.Content)}";
        }
        else
        {
            previewMessage = $"{file.FileName} is {file.ContentType} ({FormatSize(file.ContentLength)}) - download it to open it.";
        }
    }

    private async Task SummaryAsync(
        Document document,
        bool printable)
    {
        var (model, file) = await Gateway.GetDocumentSummaryAsync(document.Id, printable);
        if (file is not null)
        {
            // A model or a file: the PDF
            await SaveAsync(file.Content, file.FileName ?? "summary.pdf", file.ContentType ?? "application/pdf");
            return;
        }

        summary = model;
    }

    private async Task SaveAsync(
        byte[] content,
        string fileName,
        string contentType)
        => await JavaScriptRuntime.InvokeVoidAsync(
            "eval",
            $"(() => {{ const a = document.createElement('a'); a.href = 'data:{contentType};base64,{Convert.ToBase64String(content)}'; a.download = {System.Text.Json.JsonSerializer.Serialize(fileName)}; document.body.appendChild(a); a.click(); a.remove(); }})()");

    private static string FormatSize(long bytes)
        => bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB",
        };
}