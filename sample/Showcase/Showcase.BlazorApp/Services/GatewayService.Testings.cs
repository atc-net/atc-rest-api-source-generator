#pragma warning disable IDE0001
namespace Showcase.BlazorApp.Services;

/// <summary>
/// Gateway service - Testings operations using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    /// <summary>
    /// Test exception handling by code.
    /// Returns success response or throws with HTTP status info.
    /// </summary>
    public async Task<(bool IsSuccess, int StatusCode, string Message, Showcase.Generated.Testing.Models.ExceptionTestResponse? Response)> TestExceptionAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Showcase.Generated.Testing.Client.GetExceptionTestParameters(Code: code);

        // Use "Showcase-Testing" HttpClient which has NO resilience/retry configured
        // This ensures we get the actual error response without retry interference
        var result = await getExceptionTestEndpoint
            .ExecuteAsync(
                parameters,
                httpClientName: "Showcase-Testing",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsOk)
        {
            return (true, (int)result.StatusCode, result.OkContent.Message, result.OkContent);
        }

        if (result.IsNotFound)
        {
            return (false, (int)result.StatusCode, "Not Found - Invalid code", null);
        }

        // For other status codes (400, 401, 409, 500, 501, 504), read the error message
        var statusCode = (int)result.StatusCode;
        var message = $"Error: {result.StatusCode}";

        switch (result.ContentObject)
        {
            // ContentObject may be: ProblemDetails (if registered), string (raw JSON), or JsonElement
            // Since only 200/404 are registered, other status codes come as raw content
            case Showcase.Generated.ProblemDetails problemDetails:
                message = problemDetails.Detail ?? problemDetails.Title ?? message;
                break;
            case string jsonString when !string.IsNullOrEmpty(jsonString):
                // Try to deserialize raw JSON string to ProblemDetails
                try
                {
                    var pd = JsonSerializer.Deserialize<Showcase.Generated.ProblemDetails>(jsonString, jsonOptions);
                    if (pd is not null)
                    {
                        message = pd.Detail ?? pd.Title ?? message;
                    }
                }
                catch
                {
                    // If deserialization fails, use the raw string
                    message = jsonString;
                }

                break;
            default:
            {
                if (result.ContentObject is not null)
                {
                    message = result.ContentObject.ToString() ?? message;
                }

                break;
            }
        }

        return (false, statusCode, message, null);
    }
}