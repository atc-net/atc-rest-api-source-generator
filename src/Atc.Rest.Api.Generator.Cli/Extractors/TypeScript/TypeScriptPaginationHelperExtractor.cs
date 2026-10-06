namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates a pagination helper utility for automatically iterating through paginated API responses.
/// Works with any PaginatedResult-style response that has a continuation token or page index.
/// </summary>
public static class TypeScriptPaginationHelperExtractor
{
    /// <summary>
    /// Generates the paginate.ts utility file content.
    /// </summary>
    public static string Generate(string? headerContent)
    {
        var sb = new StringBuilder();

        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        sb.AppendLine("import type { ApiResult } from '../types/ApiResult';");
        sb.AppendLine();

        // PaginatedResponse interface
        sb.AppendLine("/** Shape of a paginated API response with a results array and optional continuation. */");
        sb.AppendLine("export interface PaginatedResponse<T> {");
        sb.AppendLine("  readonly results?: T[];");
        sb.AppendLine("  readonly items?: T[];");
        sb.AppendLine("  readonly continuation?: string | null;");
        sb.AppendLine("  readonly pageIndex?: number;");
        sb.AppendLine("  readonly pageSize?: number;");
        sb.AppendLine("  readonly totalCount?: number;");
        sb.AppendLine("}");
        sb.AppendLine();

        // The names of the paging parameters the endpoint takes
        sb.AppendLine("/** The names of the parameters the next page is asked for with (`continuation` and `pageIndex` by default). */");
        sb.AppendLine("export interface PaginateAllOptions {");
        sb.AppendLine("  readonly continuationParam?: string;");
        sb.AppendLine("  readonly pageIndexParam?: string;");
        sb.AppendLine("}");
        sb.AppendLine();

        // paginateAll function
        sb.AppendLine("/**");
        sb.AppendLine(" * Auto-paginates through all pages of a paginated API endpoint.");
        sb.AppendLine(" * Yields items one at a time as an AsyncGenerator.");
        sb.AppendLine(" *");
        sb.AppendLine(" * @example");
        sb.AppendLine(" * ```typescript");
        sb.AppendLine(" * for await (const account of paginateAll(");
        sb.AppendLine(" *   (params) => api.accounts.listPaginatedAccounts(params),");
        sb.AppendLine(" *   { pageSize: 20 }");
        sb.AppendLine(" * )) {");
        sb.AppendLine(" *   console.log(account.name);");
        sb.AppendLine(" * }");
        sb.AppendLine(" * ```");
        sb.AppendLine(" */");
        sb.AppendLine("export async function* paginateAll<T, P extends Record<string, unknown>>(");
        sb.AppendLine("  fetcher: (params: P) => Promise<ApiResult<PaginatedResponse<T>>>,");
        sb.AppendLine("  initialParams: P,");
        sb.AppendLine("  options?: PaginateAllOptions,");
        sb.AppendLine("): AsyncGenerator<T> {");
        sb.AppendLine("  const continuationParam = options?.continuationParam ?? 'continuation';");
        sb.AppendLine("  const pageIndexParam = options?.pageIndexParam ?? 'pageIndex';");
        sb.AppendLine("  let params = { ...initialParams };");
        sb.AppendLine();
        sb.AppendLine("  while (true) {");
        sb.AppendLine("    const result = await fetcher(params);");
        sb.AppendLine();
        sb.AppendLine("    // A page that failed is an error, never a shorter list");
        sb.AppendLine("    if (result.status !== 'ok' && result.status !== 'created') {");
        sb.AppendLine("      throw 'error' in result ? result.error : new Error(`Pagination stopped on a ${result.status} response`);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const page = result.data;");
        sb.AppendLine("    const items = page.results ?? page.items ?? [];");
        sb.AppendLine();
        sb.AppendLine("    for (const item of items) {");
        sb.AppendLine("      yield item;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Stop if no continuation token or empty page");
        sb.AppendLine("    if (!page.continuation && page.pageIndex == null) {");
        sb.AppendLine("      break;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (items.length === 0) {");
        sb.AppendLine("      break;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // A token equal to the one this page was asked for with would page forever");
        sb.AppendLine("    if (page.continuation && page.continuation === params[continuationParam]) {");
        sb.AppendLine("      break;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Advance to next page");
        sb.AppendLine("    if (page.continuation) {");
        sb.AppendLine("      params = { ...params, [continuationParam]: page.continuation } as P;");
        sb.AppendLine("    } else if (page.pageIndex != null) {");
        sb.AppendLine("      params = { ...params, [pageIndexParam]: (page.pageIndex + 1) } as P;");
        sb.AppendLine("    } else {");
        sb.AppendLine("      break;");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // collectAll convenience
        sb.AppendLine("/**");
        sb.AppendLine(" * Collects all items from a paginated endpoint into a single array.");
        sb.AppendLine(" *");
        sb.AppendLine(" * @example");
        sb.AppendLine(" * ```typescript");
        sb.AppendLine(" * const allAccounts = await collectAll(");
        sb.AppendLine(" *   (params) => api.accounts.listPaginatedAccounts(params),");
        sb.AppendLine(" *   { pageSize: 50 }");
        sb.AppendLine(" * );");
        sb.AppendLine(" * ```");
        sb.AppendLine(" */");
        sb.AppendLine("export async function collectAll<T, P extends Record<string, unknown>>(");
        sb.AppendLine("  fetcher: (params: P) => Promise<ApiResult<PaginatedResponse<T>>>,");
        sb.AppendLine("  initialParams: P,");
        sb.AppendLine("  options?: PaginateAllOptions,");
        sb.AppendLine("): Promise<T[]> {");
        sb.AppendLine("  const items: T[] = [];");
        sb.AppendLine("  for await (const item of paginateAll(fetcher, initialParams, options)) {");
        sb.AppendLine("    items.push(item);");
        sb.AppendLine("  }");
        sb.AppendLine("  return items;");
        sb.Append('}');

        return sb.ToString();
    }
}