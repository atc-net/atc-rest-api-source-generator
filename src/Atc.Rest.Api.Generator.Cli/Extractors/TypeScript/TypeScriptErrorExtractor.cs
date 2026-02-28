namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates static error type files (ApiError.ts and ValidationError.ts).
/// </summary>
public static class TypeScriptErrorExtractor
{
    /// <summary>
    /// Generates the content for ApiError.ts.
    /// </summary>
    /// <param name="headerContent">Optional auto-generated file header.</param>
    /// <param name="httpClient">The HTTP client library to use.</param>
    /// <returns>The TypeScript file content.</returns>
    public static string GenerateApiError(
        string? headerContent,
        TypeScriptHttpClient httpClient = TypeScriptHttpClient.Fetch)
    {
        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        var responseType = httpClient == TypeScriptHttpClient.Axios ? "AxiosResponse" : "Response";

        if (httpClient == TypeScriptHttpClient.Axios)
        {
            sb.AppendLine("import type { AxiosResponse } from 'axios';");
            sb.AppendLine();
        }

        sb.AppendLine("export class ApiError extends Error {");
        sb.AppendLine("  readonly status: number;");
        sb.AppendLine("  readonly statusText: string;");
        sb.Append("  readonly response?: ").Append(responseType).AppendLine(";");
        sb.AppendLine();
        sb.Append("  constructor(status: number, statusText: string, message: string, response?: ").Append(responseType).AppendLine(") {");
        sb.AppendLine("    super(message);");
        sb.AppendLine("    this.name = 'ApiError';");
        sb.AppendLine("    this.status = status;");
        sb.AppendLine("    this.statusText = statusText;");
        sb.AppendLine("    this.response = response;");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the content for ValidationError.ts.
    /// </summary>
    /// <param name="headerContent">Optional auto-generated file header.</param>
    /// <param name="httpClient">The HTTP client library to use.</param>
    /// <returns>The TypeScript file content.</returns>
    public static string GenerateValidationError(
        string? headerContent,
        TypeScriptHttpClient httpClient = TypeScriptHttpClient.Fetch)
    {
        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        var responseType = httpClient == TypeScriptHttpClient.Axios ? "AxiosResponse" : "Response";

        if (httpClient == TypeScriptHttpClient.Axios)
        {
            sb.AppendLine("import type { AxiosResponse } from 'axios';");
        }

        sb.AppendLine("import { ApiError } from './ApiError';");
        sb.AppendLine();
        sb.AppendLine("export class ValidationError extends ApiError {");
        sb.AppendLine("  readonly errors: Record<string, string[]>;");
        sb.AppendLine();
        sb.AppendLine("  constructor(");
        sb.AppendLine("    status: number,");
        sb.AppendLine("    statusText: string,");
        sb.AppendLine("    message: string,");
        sb.AppendLine("    errors: Record<string, string[]>,");
        sb.Append("    response?: ").Append(responseType).AppendLine(",");
        sb.AppendLine("  ) {");
        sb.AppendLine("    super(status, statusText, message, response);");
        sb.AppendLine("    this.name = 'ValidationError';");
        sb.AppendLine("    this.errors = errors;");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}