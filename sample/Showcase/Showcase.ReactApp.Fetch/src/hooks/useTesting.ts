import { useState, useCallback } from 'react';
import { testingClient } from '../config/api';
import { ApiError, ValidationError } from '../api/errors';

export interface TestResult {
  code: number;
  expectedStatus: number;
  actualStatus: number;
  exceptionType?: string;
  message?: string;
  match: boolean;
  timestamp: string;
}

/** What the client read from one error response (an RFC 9457 ProblemDetails). */
export interface ErrorResponseResult {
  status: number;
  arm: string;
  title?: string;
  detail?: string;
  errors?: string;
  retryAfter?: string;
  timestamp: string;
}

// Mirrors the exception codes in Showcase.yaml (getExceptionTest) and the
// Atc.Rest.MinimalApi 3.0 GlobalErrorHandlingMiddleware status mapping.
export const exceptionTests = [
  { code: 0, exceptionType: 'No Exception', expectedStatus: 200 },
  { code: 1, exceptionType: 'Exception', expectedStatus: 500 },
  { code: 2, exceptionType: 'ArgumentException', expectedStatus: 400 },
  { code: 3, exceptionType: 'ArgumentNullException', expectedStatus: 500 },
  { code: 4, exceptionType: 'InvalidOperationException', expectedStatus: 500 },
  { code: 5, exceptionType: 'UnauthorizedAccessException', expectedStatus: 500 },
  { code: 6, exceptionType: 'NotImplementedException', expectedStatus: 501 },
  { code: 7, exceptionType: 'TimeoutException', expectedStatus: 504 },
  { code: 8, exceptionType: 'ValidationException', expectedStatus: 400 },
  { code: 9, exceptionType: 'KeyNotFoundException', expectedStatus: 500 },
  { code: 10, exceptionType: 'NullReferenceException', expectedStatus: 500 },
  { code: 11, exceptionType: 'TaskCanceledException', expectedStatus: 504 },
] as const;

export const errorStatuses = [400, 404, 409, 422, 429] as const;

export function useTesting() {
  const [results, setResults] = useState<TestResult[]>([]);
  const [errorResults, setErrorResults] = useState<ErrorResponseResult[]>([]);
  const [isRunning, setIsRunning] = useState(false);

  const runTest = useCallback(async (code: number) => {
    const expectedStatus = exceptionTests.find((t) => t.code === code)?.expectedStatus ?? 0;
    const result = await testingClient.getExceptionTest(code);

    const actualStatus = result.response.status;
    const testResult: TestResult = {
      code,
      expectedStatus,
      actualStatus,
      match: actualStatus === expectedStatus,
      timestamp: new Date().toISOString(),
    };

    if ('data' in result) {
      testResult.message = result.data.message;
    } else if ('error' in result) {
      testResult.exceptionType = result.error.name;
      // ApiError.message is the ProblemDetails `detail` (the server's message), falling back to `title`.
      testResult.message = result.error.message;
    }

    setResults((prev) => [...prev, testResult]);
  }, []);

  const runAll = useCallback(async () => {
    setIsRunning(true);
    try {
      for (const test of exceptionTests) {
        await runTest(test.code);
      }
    } finally {
      setIsRunning(false);
    }
  }, [runTest]);

  const runErrorResponseTest = useCallback(async (status: number) => {
    setIsRunning(true);
    try {
      const result = await testingClient.getErrorResponseTest(status);
      const entry: ErrorResponseResult = {
        status: result.response.status,
        arm: result.status,
        retryAfter: result.response.headers.get('Retry-After') ?? undefined,
        timestamp: new Date().toISOString(),
      };

      if ('error' in result && result.error instanceof ApiError) {
        entry.title = result.error.title;
        entry.detail = result.error.detail;
        if (result.error instanceof ValidationError) {
          entry.errors = Object.entries(result.error.errors)
            .map(([key, messages]) => `${key}: ${messages.join(', ')}`)
            .join('; ');
        }
      }

      setErrorResults((prev) => [entry, ...prev]);
    } finally {
      setIsRunning(false);
    }
  }, []);

  const clear = useCallback(() => {
    setResults([]);
    setErrorResults([]);
  }, []);

  return { results, errorResults, isRunning, runTest, runAll, runErrorResponseTest, clear };
}
