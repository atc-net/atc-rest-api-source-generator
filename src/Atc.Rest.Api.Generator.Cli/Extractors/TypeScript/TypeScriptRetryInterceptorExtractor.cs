namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates retryInterceptor.ts containing the retry-with-backoff utility function.
/// Supports exponential, linear, and constant backoff strategies with jitter,
/// Retry-After header handling for 429 and 503 responses, and AbortSignal cancellation.
/// </summary>
public static class TypeScriptRetryInterceptorExtractor
{
    /// <summary>
    /// Generates the retry interceptor TypeScript file.
    /// </summary>
    /// <param name="headerContent">Optional auto-generated header.</param>
    /// <returns>The generated TypeScript source code.</returns>
    public static string Generate(string? headerContent)
        => headerContent + TemplateText.Lines(
        [
            """
            import type { RequestRetry, RetryPolicy } from './retryConfig';

            /** The longest Retry-After a 429 or 503 may ask for when the policy has no timeout; longer goes back to the caller. */
            const MAX_RETRY_AFTER_MS = 30000;

            """,

            // resolveRetryPolicy: the single place the method rule and per-operation settings meet
            """
            /**
             * The policy for one request, or undefined for no retry handling at all.
             * - The ApiClient's `retryPolicy: false` switches retry off for every request.
             * - POST and PATCH are not retried unless the operation opts in (`retry.enabled: true`).
             * - "No retry" keeps the policy's timeout: the request gets one attempt.
             */
            export function resolveRetryPolicy(
              method: string,
              clientPolicy: RetryPolicy | false,
              retry?: RequestRetry,
            ): RetryPolicy | undefined {
              if (clientPolicy === false) return undefined;

              const policy = retry?.policy ?? clientPolicy;
              const upper = method.toUpperCase();
              const enabled = retry?.enabled ?? (upper !== 'POST' && upper !== 'PATCH');
              const effective: RetryPolicy = enabled ? policy : { ...policy, maxAttempts: 0 };
              return retry?.timeoutMs !== undefined ? { ...effective, timeoutMs: retry.timeoutMs } : effective;
            }

            """,

            // sleep helper
            """
            /** Delays execution for the specified number of milliseconds. */
            function sleep(ms: number, signal?: AbortSignal): Promise<void> {
              return new Promise((resolve, reject) => {
                if (signal?.aborted) {
                  reject(signal.reason ?? new DOMException('Aborted', 'AbortError'));
                  return;
                }

                const timer = setTimeout(resolve, ms);
                signal?.addEventListener('abort', () => {
                  clearTimeout(timer);
                  reject(signal.reason ?? new DOMException('Aborted', 'AbortError'));
                }, { once: true });
              });
            }

            """,

            // computeDelay helper
            """
            /** Computes the delay for a given attempt based on the backoff strategy. */
            function computeDelay(attempt: number, policy: RetryPolicy): number {
              let delay: number;

              switch (policy.backoff) {
                case 'constant':
                  delay = policy.delayMs;
                  break;
                case 'linear':
                  delay = policy.delayMs * (attempt + 1);
                  break;
                case 'exponential':
                default:
                  delay = policy.delayMs * Math.pow(2, attempt);
                  break;
              }

              if (policy.useJitter) {
                // Add jitter: randomize between 0.5x and 1.5x the computed delay
                delay = delay * (0.5 + Math.random());
              }

              return Math.round(delay);
            }

            """,

            // parseRetryAfter helper
            """
            /** Parses the Retry-After header value into milliseconds. */
            function parseRetryAfter(header: string | null): number | undefined {
              if (!header) return undefined;

              // Try parsing as seconds (integer)
              const seconds = Number(header);
              if (!Number.isNaN(seconds) && seconds >= 0) {
                return Math.round(seconds * 1000);
              }

              // Try parsing as HTTP-date
              const date = Date.parse(header);
              if (!Number.isNaN(date)) {
                const delayMs = date - Date.now();
                return delayMs > 0 ? Math.round(delayMs) : 0;
              }

              return undefined;
            }

            """,

            // isRetryableStatus helper
            """
            /** Determines whether the given HTTP status code is retryable. */
            function isRetryableStatus(status: number, handle429: boolean): boolean {
              if (handle429 && status === 429) return true;
              // Transient: 408 Request Timeout and every 5xx (the same set as the generated C# client)
              return status === 408 || (status >= 500 && status <= 599);
            }

            """,

            // Main retryWithBackoff function
            """
            /**
             * Executes a fetch request with automatic retry and backoff.
             *
             * @param fetchFn - A function that performs the request. It receives a per-attempt
             *   AbortSignal that aborts when either the parent signal aborts or the per-attempt
             *   timeout fires; pass it to fetch so the in-flight request is actually cancelled.
             * @param policy - The retry policy to apply.
             * @param signal - Optional parent AbortSignal for caller-initiated cancellation.
             * @param read - Reads the response that is not retried (the first success, or the last attempt). It runs
             *   while the per-attempt signal and timeout are still live, so a body that stalls or an abort during the
             *   body read is cancelled like the request itself. Without it the Response is returned unread.
             * @returns What `read` returns: the Response from the first successful attempt or the last failed attempt.
             *
             * @example
             * ```typescript
             * const data = await retryWithBackoff(
             *   (signal) => fetch('/api/data', { signal }),
             *   { maxAttempts: 3, delayMs: 1000, backoff: 'exponential', useJitter: true, handle429: true },
             *   undefined,
             *   (response) => response.json(),
             * );
             * ```
             */
            export async function retryWithBackoff<T = Response>(
              fetchFn: (signal: AbortSignal) => Promise<Response>,
              policy: RetryPolicy,
              signal?: AbortSignal,
              read: (response: Response) => Promise<T> = (response) => Promise.resolve(response as unknown as T),
            ): Promise<T> {
              if (policy.totalTimeoutMs === undefined) {
                return attemptWithBackoff(fetchFn, policy, signal, read);
              }

              // The policy's total timeout: one deadline over every attempt and the waits between them
              const total = new AbortController();
              let totalFired = false;
              const onCallerAbort = () => total.abort(signal?.reason);
              if (signal?.aborted) {
                total.abort(signal.reason);
              } else {
                signal?.addEventListener('abort', onCallerAbort, { once: true });
              }

              const timer = setTimeout(() => {
                totalFired = true;
                total.abort(new DOMException('Total timeout', 'TimeoutError'));
              }, policy.totalTimeoutMs);

              try {
                return await attemptWithBackoff(fetchFn, policy, total.signal, read);
              } catch (error) {
                // Fails with the total timeout's TimeoutError, whatever the transport rejected with (Axios: its CanceledError)
                throw totalFired ? total.signal.reason : error;
              } finally {
                clearTimeout(timer);
                signal?.removeEventListener('abort', onCallerAbort);
              }
            }

            async function attemptWithBackoff<T>(
              fetchFn: (signal: AbortSignal) => Promise<Response>,
              policy: RetryPolicy,
              signal: AbortSignal | undefined,
              read: (response: Response) => Promise<T>,
            ): Promise<T> {
              let lastError: unknown;

              for (let attempt = 0; attempt <= policy.maxAttempts; attempt++) {
                // Per-attempt AbortController: cancels the in-flight fetch when either the
                // parent signal aborts (user-initiated) or the per-attempt timeout fires.
                // Without wiring this through to fetchFn, policy.timeoutMs would fire locally
                // but the network request would run to completion.
                const controller = new AbortController();
                const onParentAbort = () => controller.abort(signal?.reason);
                if (signal?.aborted) {
                  controller.abort(signal.reason);
                } else {
                  signal?.addEventListener('abort', onParentAbort, { once: true });
                }

                let timeoutFired = false;
                let timer: ReturnType<typeof setTimeout> | undefined;
                if (policy.timeoutMs) {
                  timer = setTimeout(() => {
                    timeoutFired = true;
                    controller.abort(new DOMException('Per-attempt timeout', 'TimeoutError'));
                  }, policy.timeoutMs);
                }

                try {
                  // Check for cancellation before each attempt
                  signal?.throwIfAborted();

                  const response = await fetchFn(controller.signal);

                  // Success — no retry needed; the body is read while this attempt's signal and timeout are still live.
                  if (!isRetryableStatus(response.status, policy.handle429)) {
                    return await read(response);
                  }

                  // Last attempt — read as-is
                  if (attempt === policy.maxAttempts) {
                    return await read(response);
                  }

                  // A 429 or 503 may say how long to wait (Retry-After, RFC 9110 10.2.3); another status's Retry-After is not read
                  let delay: number;
                  if (response.status === 429 || response.status === 503) {
                    const retryAfterMs = parseRetryAfter(response.headers.get('Retry-After'));
                    // Asking for a longer wait than the policy allows goes back to the caller.
                    if (retryAfterMs !== undefined && retryAfterMs > (policy.timeoutMs ?? MAX_RETRY_AFTER_MS)) {
                      return await read(response);
                    }

                    delay = retryAfterMs ?? computeDelay(attempt, policy);
                  } else {
                    delay = computeDelay(attempt, policy);
                  }

                  // The attempt is retried: its body is not read, so release the connection.
                  void response.body?.cancel().catch(() => undefined);

                  await sleep(delay, signal);
                } catch (error) {
                  // Caller-initiated aborts always propagate (whatever the abort reason, e.g. Axios' CanceledError);
                  // per-attempt timeouts are retryable.
                  if (signal?.aborted || (error instanceof DOMException && error.name === 'AbortError' && !timeoutFired)) {
                    throw error;
                  }

                  // A timed-out attempt fails with the timeout's TimeoutError, whatever the transport rejected with: Axios
                  // rejects every abort with its CanceledError, which would make a timeout look like the caller's cancellation.
                  lastError = timeoutFired ? controller.signal.reason : error;

                  // Last attempt — rethrow
                  if (attempt === policy.maxAttempts) {
                    throw lastError;
                  }

                  await sleep(computeDelay(attempt, policy), signal);
                } finally {
                  if (timer !== undefined) {
                    clearTimeout(timer);
                  }

                  signal?.removeEventListener('abort', onParentAbort);
                }
              }

              // Should not reach here (the last attempt returns or throws), but satisfy TypeScript
              throw lastError;
            """,
        ]) + '}';
}