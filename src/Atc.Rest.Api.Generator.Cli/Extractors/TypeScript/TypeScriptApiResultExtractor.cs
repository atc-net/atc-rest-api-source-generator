namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates the ApiResult.ts file with discriminated union type and type guards.
/// </summary>
public static class TypeScriptApiResultExtractor
{
    /// <summary>
    /// Generates the content for ApiResult.ts.
    /// </summary>
    /// <param name="headerContent">Optional auto-generated file header.</param>
    /// <returns>The TypeScript file content.</returns>
    public static string Generate(string? headerContent)
    {
        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        // Imports
        sb.AppendLine("import type { ApiError } from '../errors/ApiError';");
        sb.AppendLine("import type { ValidationError } from '../errors/ValidationError';");
        sb.AppendLine();

        // Discriminated union type
        sb.AppendLine("export type ApiResult<T> =");
        sb.AppendLine("  | { status: 'ok'; data: T; response: Response }");
        sb.AppendLine("  | { status: 'created'; data: T; response: Response }");
        sb.AppendLine("  | { status: 'noContent'; response: Response }");
        sb.AppendLine("  | { status: 'badRequest'; error: ValidationError; response: Response }");
        sb.AppendLine("  | { status: 'unauthorized'; error: ApiError; response: Response }");
        sb.AppendLine("  | { status: 'notFound'; error: ApiError; response: Response }");
        sb.AppendLine("  | { status: 'conflict'; error: ApiError; response: Response }");
        sb.AppendLine("  | { status: 'serverError'; error: ApiError; response: Response };");
        sb.AppendLine();

        // Type guard functions
        AppendTypeGuard(sb, "isOk", "ok", hasData: true);
        AppendTypeGuard(sb, "isCreated", "created", hasData: true);
        AppendTypeGuard(sb, "isNoContent", "noContent", hasData: false);
        AppendErrorTypeGuard(sb, "isBadRequest", "badRequest", "ValidationError");
        AppendErrorTypeGuard(sb, "isUnauthorized", "unauthorized", "ApiError");
        AppendErrorTypeGuard(sb, "isNotFound", "notFound", "ApiError");
        AppendErrorTypeGuard(sb, "isConflict", "conflict", "ApiError");
        AppendErrorTypeGuard(sb, "isServerError", "serverError", "ApiError");

        return sb.ToString();
    }

    private static void AppendTypeGuard(
        StringBuilder sb,
        string functionName,
        string statusValue,
        bool hasData)
    {
        var narrowedType = hasData
            ? string.Concat("{ status: '", statusValue, "'; data: T; response: Response }")
            : string.Concat("{ status: '", statusValue, "'; response: Response }");

        sb.Append("export function ").Append(functionName).Append("<T>(result: ApiResult<T>): result is ").Append(narrowedType).AppendLine(" {");
        sb.Append("  return result.status === '").Append(statusValue).AppendLine("';");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendErrorTypeGuard(
        StringBuilder sb,
        string functionName,
        string statusValue,
        string errorType)
    {
        sb.Append("export function ").Append(functionName).Append("<T>(result: ApiResult<T>): result is { status: '").Append(statusValue).Append("'; error: ").Append(errorType).AppendLine("; response: Response } {");
        sb.Append("  return result.status === '").Append(statusValue).AppendLine("';");
        sb.AppendLine("}");
        sb.AppendLine();
    }
}