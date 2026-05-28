namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates retryInterceptor.ts containing the retry-with-backoff utility function.
/// Supports exponential, linear, and constant backoff strategies with jitter,
/// Retry-After header handling for 429 responses, and AbortSignal cancellation.
/// </summary>
public static class TypeScriptRetryInterceptorExtractor
{
    /// <summary>
    /// Generates the retry interceptor TypeScript file.
    /// </summary>
    /// <param name="headerContent">Optional auto-generated header.</param>
    /// <returns>The generated TypeScript source code.</returns>
    public static string Generate(string? headerContent)
    {
        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        sb.AppendLine("import type { RetryPolicy } from './retryConfig';");
        sb.AppendLine();

        // sleep helper
        sb.AppendLine("/** Delays execution for the specified number of milliseconds. */");
        sb.AppendLine("function sleep(ms: number, signal?: AbortSignal): Promise<void> {");
        sb.AppendLine("  return new Promise((resolve, reject) => {");
        sb.AppendLine("    const timer = setTimeout(resolve, ms);");
        sb.AppendLine("    signal?.addEventListener('abort', () => {");
        sb.AppendLine("      clearTimeout(timer);");
        sb.AppendLine("      reject(signal.reason ?? new DOMException('Aborted', 'AbortError'));");
        sb.AppendLine("    }, { once: true });");
        sb.AppendLine("  });");
        sb.AppendLine("}");
        sb.AppendLine();

        // computeDelay helper
        sb.AppendLine("/** Computes the delay for a given attempt based on the backoff strategy. */");
        sb.AppendLine("function computeDelay(attempt: number, policy: RetryPolicy): number {");
        sb.AppendLine("  let delay: number;");
        sb.AppendLine();
        sb.AppendLine("  switch (policy.backoff) {");
        sb.AppendLine("    case 'constant':");
        sb.AppendLine("      delay = policy.delayMs;");
        sb.AppendLine("      break;");
        sb.AppendLine("    case 'linear':");
        sb.AppendLine("      delay = policy.delayMs * (attempt + 1);");
        sb.AppendLine("      break;");
        sb.AppendLine("    case 'exponential':");
        sb.AppendLine("    default:");
        sb.AppendLine("      delay = policy.delayMs * Math.pow(2, attempt);");
        sb.AppendLine("      break;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  if (policy.useJitter) {");
        sb.AppendLine("    // Add jitter: randomize between 0.5x and 1.5x the computed delay");
        sb.AppendLine("    delay = delay * (0.5 + Math.random());");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  return Math.round(delay);");
        sb.AppendLine("}");
        sb.AppendLine();

        // parseRetryAfter helper
        sb.AppendLine("/** Parses the Retry-After header value into milliseconds. */");
        sb.AppendLine("function parseRetryAfter(header: string | null): number | undefined {");
        sb.AppendLine("  if (!header) return undefined;");
        sb.AppendLine();
        sb.AppendLine("  // Try parsing as seconds (integer)");
        sb.AppendLine("  const seconds = Number(header);");
        sb.AppendLine("  if (!Number.isNaN(seconds) && seconds >= 0) {");
        sb.AppendLine("    return Math.round(seconds * 1000);");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  // Try parsing as HTTP-date");
        sb.AppendLine("  const date = Date.parse(header);");
        sb.AppendLine("  if (!Number.isNaN(date)) {");
        sb.AppendLine("    const delayMs = date - Date.now();");
        sb.AppendLine("    return delayMs > 0 ? Math.round(delayMs) : 0;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  return undefined;");
        sb.AppendLine("}");
        sb.AppendLine();

        // isRetryableStatus helper
        sb.AppendLine("/** Determines whether the given HTTP status code is retryable. */");
        sb.AppendLine("function isRetryableStatus(status: number, handle429: boolean): boolean {");
        sb.AppendLine("  if (handle429 && status === 429) return true;");
        sb.AppendLine("  // Retry on server errors (500, 502, 503, 504)");
        sb.AppendLine("  return status >= 500 && status <= 504;");
        sb.AppendLine("}");
        sb.AppendLine();

        // Main retryWithBackoff function
        sb.AppendLine("/**");
        sb.AppendLine(" * Executes a fetch request with automatic retry and backoff.");
        sb.AppendLine(" *");
        sb.AppendLine(" * @param fetchFn - A function that performs the request. It receives a per-attempt");
        sb.AppendLine(" *   AbortSignal that aborts when either the parent signal aborts or the per-attempt");
        sb.AppendLine(" *   timeout fires; pass it to fetch so the in-flight request is actually cancelled.");
        sb.AppendLine(" * @param policy - The retry policy to apply.");
        sb.AppendLine(" * @param signal - Optional parent AbortSignal for caller-initiated cancellation.");
        sb.AppendLine(" * @returns The Response from the first successful attempt or the last failed attempt.");
        sb.AppendLine(" *");
        sb.AppendLine(" * @example");
        sb.AppendLine(" * ```typescript");
        sb.AppendLine(" * const response = await retryWithBackoff(");
        sb.AppendLine(" *   (signal) => fetch('/api/data', { signal }),");
        sb.AppendLine(" *   { maxAttempts: 3, delayMs: 1000, backoff: 'exponential', useJitter: true, handle429: true }");
        sb.AppendLine(" * );");
        sb.AppendLine(" * ```");
        sb.AppendLine(" */");
        sb.AppendLine("export async function retryWithBackoff(");
        sb.AppendLine("  fetchFn: (signal: AbortSignal) => Promise<Response>,");
        sb.AppendLine("  policy: RetryPolicy,");
        sb.AppendLine("  signal?: AbortSignal,");
        sb.AppendLine("): Promise<Response> {");
        sb.AppendLine("  let lastResponse: Response | undefined;");
        sb.AppendLine("  let lastError: unknown;");
        sb.AppendLine();
        sb.AppendLine("  for (let attempt = 0; attempt <= policy.maxAttempts; attempt++) {");
        sb.AppendLine("    // Per-attempt AbortController: cancels the in-flight fetch when either the");
        sb.AppendLine("    // parent signal aborts (user-initiated) or the per-attempt timeout fires.");
        sb.AppendLine("    // Without wiring this through to fetchFn, policy.timeoutMs would fire locally");
        sb.AppendLine("    // but the network request would run to completion.");
        sb.AppendLine("    const controller = new AbortController();");
        sb.AppendLine("    const onParentAbort = () => controller.abort(signal?.reason);");
        sb.AppendLine("    if (signal?.aborted) {");
        sb.AppendLine("      controller.abort(signal.reason);");
        sb.AppendLine("    } else {");
        sb.AppendLine("      signal?.addEventListener('abort', onParentAbort, { once: true });");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    let timeoutFired = false;");
        sb.AppendLine("    let timer: ReturnType<typeof setTimeout> | undefined;");
        sb.AppendLine("    if (policy.timeoutMs) {");
        sb.AppendLine("      timer = setTimeout(() => {");
        sb.AppendLine("        timeoutFired = true;");
        sb.AppendLine("        controller.abort(new DOMException('Per-attempt timeout', 'TimeoutError'));");
        sb.AppendLine("      }, policy.timeoutMs);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    try {");
        sb.AppendLine("      // Check for cancellation before each attempt");
        sb.AppendLine("      signal?.throwIfAborted();");
        sb.AppendLine();
        sb.AppendLine("      const response = await fetchFn(controller.signal);");
        sb.AppendLine();
        sb.AppendLine("      lastResponse = response;");
        sb.AppendLine();
        sb.AppendLine("      // Success — no retry needed");
        sb.AppendLine("      if (!isRetryableStatus(response.status, policy.handle429)) {");
        sb.AppendLine("        return response;");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      // Last attempt — return as-is");
        sb.AppendLine("      if (attempt === policy.maxAttempts) {");
        sb.AppendLine("        return response;");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      // Handle 429 with Retry-After header");
        sb.AppendLine("      let delay: number;");
        sb.AppendLine("      if (response.status === 429 && policy.handle429) {");
        sb.AppendLine("        const retryAfterMs = parseRetryAfter(response.headers.get('Retry-After'));");
        sb.AppendLine("        delay = retryAfterMs ?? computeDelay(attempt, policy);");
        sb.AppendLine("      } else {");
        sb.AppendLine("        delay = computeDelay(attempt, policy);");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      await sleep(delay, signal);");
        sb.AppendLine("    } catch (error) {");
        sb.AppendLine("      // Caller-initiated aborts always propagate; per-attempt timeouts are retryable.");
        sb.AppendLine("      if (error instanceof DOMException && error.name === 'AbortError' && !timeoutFired) {");
        sb.AppendLine("        throw error;");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      lastError = error;");
        sb.AppendLine();
        sb.AppendLine("      // Last attempt — rethrow");
        sb.AppendLine("      if (attempt === policy.maxAttempts) {");
        sb.AppendLine("        throw error;");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      await sleep(computeDelay(attempt, policy), signal);");
        sb.AppendLine("    } finally {");
        sb.AppendLine("      if (timer !== undefined) {");
        sb.AppendLine("        clearTimeout(timer);");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      signal?.removeEventListener('abort', onParentAbort);");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  // Should not reach here, but satisfy TypeScript");
        sb.AppendLine("  if (lastResponse) return lastResponse;");
        sb.AppendLine("  throw lastError;");
        sb.Append('}');

        return sb.ToString();
    }
}