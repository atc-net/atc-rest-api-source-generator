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
        => headerContent + TemplateText.Lines(
        [
            """
            import type { ApiResult } from '../types/ApiResult';

            """,

            // PaginatedResponse interface
            """
            /** Shape of a paginated API response with a results array and optional continuation. */
            export interface PaginatedResponse<T> {
              readonly results?: T[];
              readonly items?: T[];
              readonly continuation?: string | null;
              readonly pageIndex?: number;
              readonly pageSize?: number;
              readonly totalCount?: number;
            }

            """,

            // The names of the paging parameters the endpoint takes
            """
            /** The names of the parameters the next page is asked for with (`continuation` and `pageIndex` by default). */
            export interface PaginateAllOptions {
              readonly continuationParam?: string;
              readonly pageIndexParam?: string;
            }

            """,

            // paginateAll function
            """
            /**
             * Auto-paginates through all pages of a paginated API endpoint.
             * Yields items one at a time as an AsyncGenerator.
             *
             * @example
             * ```typescript
             * for await (const account of paginateAll(
             *   (params) => api.accounts.listPaginatedAccounts(params),
             *   { pageSize: 20 }
             * )) {
             *   console.log(account.name);
             * }
             * ```
             */
            export async function* paginateAll<T, P extends Record<string, unknown>>(
              fetcher: (params: P) => Promise<ApiResult<PaginatedResponse<T>>>,
              initialParams: P,
              options?: PaginateAllOptions,
            ): AsyncGenerator<T> {
              const continuationParam = options?.continuationParam ?? 'continuation';
              const pageIndexParam = options?.pageIndexParam ?? 'pageIndex';
              let params = { ...initialParams };

              while (true) {
                const result = await fetcher(params);

                // A page that failed is an error, never a shorter list
                if (result.status !== 'ok' && result.status !== 'created') {
                  throw 'error' in result ? result.error : new Error(`Pagination stopped on a ${result.status} response`);
                }

                const page = result.data;
                const items = page.results ?? page.items ?? [];

                for (const item of items) {
                  yield item;
                }

                // Stop if no continuation token or empty page
                if (!page.continuation && page.pageIndex == null) {
                  break;
                }

                if (items.length === 0) {
                  break;
                }

                // A token equal to the one this page was asked for with would page forever
                if (page.continuation && page.continuation === params[continuationParam]) {
                  break;
                }

                // Advance to next page
                if (page.continuation) {
                  params = { ...params, [continuationParam]: page.continuation } as P;
                } else if (page.pageIndex != null) {
                  params = { ...params, [pageIndexParam]: (page.pageIndex + 1) } as P;
                } else {
                  break;
                }
              }
            }

            """,

            // collectAll convenience
            """
            /**
             * Collects all items from a paginated endpoint into a single array.
             *
             * @example
             * ```typescript
             * const allAccounts = await collectAll(
             *   (params) => api.accounts.listPaginatedAccounts(params),
             *   { pageSize: 50 }
             * );
             * ```
             */
            export async function collectAll<T, P extends Record<string, unknown>>(
              fetcher: (params: P) => Promise<ApiResult<PaginatedResponse<T>>>,
              initialParams: P,
              options?: PaginateAllOptions,
            ): Promise<T[]> {
              const items: T[] = [];
              for await (const item of paginateAll(fetcher, initialParams, options)) {
                items.push(item);
              }
              return items;
            """,
        ]) + '}';
}