namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// The <c>fitToArms</c> function of the generated ApiClient (Fetch and Axios). <c>handleResponse</c> maps a
/// response to the generic <c>ApiResult</c> arms; each client method passes the arms of its
/// per-operation union (<c>arms</c>), and a status the union does not declare is folded into one it does:
/// an undeclared error status into <c>clientError</c> (4xx) or <c>serverError</c> (5xx), an undeclared
/// success status into the declared success arm. So an exhaustive <c>switch</c> over the union sees every result.
/// </summary>
internal static class TypeScriptResultArms
{
    /// <summary>
    /// Appends the module-level <c>fitToArms</c> function.
    /// </summary>
    /// <param name="sb">The string builder of the ApiClient file.</param>
    public static void AppendFitToArms(StringBuilder sb)
    {
        ArgumentNullException.ThrowIfNull(sb);

        sb.AppendLine("const httpErrorArms: readonly string[] = ['badRequest', 'unauthorized', 'forbidden', 'notFound', 'conflict', 'unprocessableEntity', 'tooManyRequests', 'clientError', 'serverError'];");
        sb.AppendLine("const successArms = ['ok', 'created', 'accepted', 'noContent'] as const;");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * Folds a result whose status the operation's union does not declare into one it does: an undeclared");
        sb.AppendLine(" * error status into clientError (4xx) or serverError (5xx), an undeclared success into the declared one.");
        sb.AppendLine(" */");
        sb.AppendLine("function fitToArms<T>(result: ApiResult<T>, arms: readonly string[] | undefined): ApiResult<T> {");
        sb.AppendLine("  if (!arms || arms.includes(result.status)) {");
        sb.AppendLine("    return result;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  if (httpErrorArms.includes(result.status) && 'error' in result && result.error instanceof ApiError) {");
        sb.AppendLine("    const status = result.error.status >= 500 ? 'serverError' : 'clientError';");
        sb.AppendLine("    return { status, error: result.error, response: result.response } as ApiResult<T>;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  const declared = successArms.find((arm) => arms.includes(arm));");
        sb.AppendLine("  if (declared === undefined || !(successArms as readonly string[]).includes(result.status)) {");
        sb.AppendLine("    return result;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  return declared === 'noContent'");
        sb.AppendLine("    ? { status: 'noContent', response: result.response } as ApiResult<T>");
        sb.AppendLine("    : { status: declared, data: ('data' in result ? result.data : undefined) as T, response: result.response } as ApiResult<T>;");
        sb.AppendLine("}");
        sb.AppendLine();
    }
}