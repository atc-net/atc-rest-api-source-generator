import { useState, useCallback } from 'react';
import { testingClient } from '../config/api';

export interface TestResult {
  code: number;
  expectedStatus: number;
  actualStatus: number;
  exceptionType?: string;
  message?: string;
  match: boolean;
  timestamp: string;
}

const expectedStatusMap: Record<number, number> = {
  0: 200,
  1: 400,
  2: 401,
  3: 403,
  4: 404,
  5: 409,
  6: 500,
  7: 500,
  8: 500,
  9: 502,
  10: 503,
};

export function useTesting() {
  const [results, setResults] = useState<TestResult[]>([]);
  const [isRunning, setIsRunning] = useState(false);

  const runTest = useCallback(async (code: number) => {
    const expectedStatus = expectedStatusMap[code] ?? 0;
    const result = await testingClient.getExceptionTest(code);

    const actualStatus = result.response.status;
    const testResult: TestResult = {
      code,
      expectedStatus,
      actualStatus,
      match: actualStatus === expectedStatus,
      timestamp: new Date().toISOString(),
    };

    if (result.status === 'ok' || result.status === 'created') {
      testResult.message = result.data.message;
    } else if ('error' in result) {
      testResult.exceptionType = result.error.name;
      testResult.message = result.error.message;
    }

    setResults((prev) => [...prev, testResult]);
  }, []);

  const runAll = useCallback(async () => {
    setIsRunning(true);
    try {
      for (let code = 0; code <= 10; code++) {
        await runTest(code);
      }
    } finally {
      setIsRunning(false);
    }
  }, [runTest]);

  const clear = useCallback(() => {
    setResults([]);
  }, []);

  return { results, isRunning, runTest, runAll, clear };
}
