import { useState, useRef, useCallback } from 'react';
import { accountsClient } from '../config/api';
import type { Account } from '../api/models/Account';

const STREAM_ITEM_DELAY_MS = 50;

function delay(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(resolve, ms);
    signal?.addEventListener('abort', () => {
      clearTimeout(timer);
      reject(new DOMException('Aborted', 'AbortError'));
    }, { once: true });
  });
}

export function useAccountsStreaming() {
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const abortControllerRef = useRef<AbortController | null>(null);

  const startStreaming = useCallback(async () => {
    if (isStreaming) return;

    const controller = new AbortController();
    abortControllerRef.current = controller;
    setIsStreaming(true);
    setAccounts([]);

    try {
      // The server streams individual Account objects via IAsyncEnumerable<Account>.
      // Each yielded value is a single Account despite the generator typing it as Accounts (Account[]).
      for await (const item of accountsClient.listAsyncEnumerableAccounts(controller.signal)) {
        if (controller.signal.aborted) break;
        const account = item as unknown as Account;
        setAccounts((prev) => [...prev, account]);
        // Small delay to visualize streaming (matches Blazor app behavior)
        await delay(STREAM_ITEM_DELAY_MS, controller.signal);
      }
    } catch (err) {
      if (!(err instanceof DOMException && err.name === 'AbortError')) {
        throw err;
      }
    } finally {
      setIsStreaming(false);
      abortControllerRef.current = null;
    }
  }, [isStreaming]);

  const cancel = useCallback(() => {
    abortControllerRef.current?.abort();
    abortControllerRef.current = null;
  }, []);

  const clear = useCallback(() => {
    setAccounts([]);
  }, []);

  return { accounts, isStreaming, startStreaming, cancel, clear };
}
