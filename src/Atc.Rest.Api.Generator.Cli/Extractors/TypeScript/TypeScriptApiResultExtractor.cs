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
    /// <param name="httpClient">The HTTP client library to use.</param>
    /// <returns>The TypeScript file content.</returns>
    public static string Generate(
        string? headerContent,
        TypeScriptHttpClient httpClient = TypeScriptHttpClient.Fetch,
        bool zodRuntimeValidate = false)
    {
        var sb = new StringBuilder();

        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        var responseType = httpClient == TypeScriptHttpClient.Axios ? "AxiosResponse" : "Response";

        // Imports
        if (httpClient == TypeScriptHttpClient.Axios)
        {
            sb.AppendLine("import type { AxiosResponse } from 'axios';");
        }

        sb.AppendLine("import type { ApiError } from '../errors/ApiError';");
        sb.AppendLine("import type { ValidationError } from '../errors/ValidationError';");

        // Zod runtime validation adds a `schemaMismatch` arm that carries the parsed
        // ZodIssue[] for diagnostics. The import is type-only so consumers that don't
        // narrow into this arm don't pay any runtime cost.
        if (zodRuntimeValidate)
        {
            sb.AppendLine("import type { ZodIssue } from 'zod';");
        }

        sb.AppendLine();

        // Discriminated union type
        sb.AppendLine("export type ApiResult<T> =");
        sb.Append("  | { status: 'ok'; data: T; response: ").Append(responseType).AppendLine(" }");
        sb.Append("  | { status: 'created'; data: T; response: ").Append(responseType).AppendLine(" }");
        sb.Append("  | { status: 'accepted'; data: T; response: ").Append(responseType).AppendLine(" }");
        sb.Append("  | { status: 'noContent'; response: ").Append(responseType).AppendLine(" }");
        sb.Append("  | { status: 'badRequest'; error: ValidationError; response: ").Append(responseType).AppendLine(" }");
        sb.Append("  | { status: 'unauthorized'; error: ApiError; response: ").Append(responseType).AppendLine(" }");
        sb.Append("  | { status: 'forbidden'; error: ApiError; response: ").Append(responseType).AppendLine(" }");
        sb.Append("  | { status: 'notFound'; error: ApiError; response: ").Append(responseType).AppendLine(" }");
        sb.Append("  | { status: 'conflict'; error: ApiError; response: ").Append(responseType).AppendLine(" }");
        sb.Append("  | { status: 'unprocessableEntity'; error: ApiError; response: ").Append(responseType).AppendLine(" }");
        sb.Append("  | { status: 'tooManyRequests'; error: ApiError; response: ").Append(responseType).AppendLine(" }");
        sb.Append("  | { status: 'serverError'; error: ApiError; response: ").Append(responseType).AppendLine(" }");
        sb.Append("  | { status: 'parseError'; error: Error; response: ").Append(responseType);
        if (zodRuntimeValidate)
        {
            sb.AppendLine(" }");
            sb.Append("  | { status: 'schemaMismatch'; issues: ZodIssue[]; data: unknown; response: ").Append(responseType).AppendLine(" };");
        }
        else
        {
            sb.AppendLine(" };");
        }

        sb.AppendLine();

        // Type guard functions
        AppendTypeGuard(sb, "isOk", "ok", hasData: true, responseType);
        AppendTypeGuard(sb, "isCreated", "created", hasData: true, responseType);
        AppendTypeGuard(sb, "isAccepted", "accepted", hasData: true, responseType);
        AppendTypeGuard(sb, "isNoContent", "noContent", hasData: false, responseType);
        AppendErrorTypeGuard(sb, "isBadRequest", "badRequest", "ValidationError", responseType);
        AppendErrorTypeGuard(sb, "isUnauthorized", "unauthorized", "ApiError", responseType);
        AppendErrorTypeGuard(sb, "isForbidden", "forbidden", "ApiError", responseType);
        AppendErrorTypeGuard(sb, "isNotFound", "notFound", "ApiError", responseType);
        AppendErrorTypeGuard(sb, "isConflict", "conflict", "ApiError", responseType);
        AppendErrorTypeGuard(sb, "isUnprocessableEntity", "unprocessableEntity", "ApiError", responseType);
        AppendErrorTypeGuard(sb, "isTooManyRequests", "tooManyRequests", "ApiError", responseType);
        AppendErrorTypeGuard(sb, "isServerError", "serverError", "ApiError", responseType);
        AppendErrorTypeGuard(sb, "isParseError", "parseError", "Error", responseType);

        if (zodRuntimeValidate)
        {
            AppendSchemaMismatchTypeGuard(sb, responseType);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Emits the <c>isSchemaMismatch</c> guard. Distinct from the other error guards
    /// because the arm carries <c>issues</c> + <c>data</c> rather than a single
    /// <c>error</c> object.
    /// </summary>
    private static void AppendSchemaMismatchTypeGuard(
        StringBuilder sb,
        string responseType)
    {
        sb.Append("export function isSchemaMismatch<T>(result: ApiResult<T>): result is { status: 'schemaMismatch'; issues: import('zod').ZodIssue[]; data: unknown; response: ").Append(responseType).AppendLine(" } {");
        sb.AppendLine("  return result.status === 'schemaMismatch';");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendTypeGuard(
        StringBuilder sb,
        string functionName,
        string statusValue,
        bool hasData,
        string responseType)
    {
        var narrowedType = hasData
            ? string.Concat("{ status: '", statusValue, "'; data: T; response: ", responseType, " }")
            : string.Concat("{ status: '", statusValue, "'; response: ", responseType, " }");

        sb.Append("export function ").Append(functionName).Append("<T>(result: ApiResult<T>): result is ").Append(narrowedType).AppendLine(" {");
        sb.Append("  return result.status === '").Append(statusValue).AppendLine("';");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendErrorTypeGuard(
        StringBuilder sb,
        string functionName,
        string statusValue,
        string errorType,
        string responseType)
    {
        sb.Append("export function ").Append(functionName).Append("<T>(result: ApiResult<T>): result is { status: '").Append(statusValue).Append("'; error: ").Append(errorType).Append("; response: ").Append(responseType).AppendLine(" } {");
        sb.Append("  return result.status === '").Append(statusValue).AppendLine("';");
        sb.AppendLine("}");
        sb.AppendLine();
    }
}