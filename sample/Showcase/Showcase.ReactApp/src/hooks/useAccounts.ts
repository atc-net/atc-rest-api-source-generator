import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { accountsClient } from '../config/api';
import { ApiError } from '../api/errors/ApiError';
import type { Account } from '../api/models/Account';

const accountKeys = {
  all: ['accounts'] as const,
  list: (limit?: number) => [...accountKeys.all, 'list', { limit }] as const,
};

export function useAccountsList(limit?: number) {
  return useQuery({
    queryKey: accountKeys.list(limit),
    queryFn: async () => {
      const result = await accountsClient.listAccounts({ limit });
      if (result.status === 'ok') {
        return result.data;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to list accounts',
        result.response,
      );
    },
    enabled: false,
  });
}

export function useCreateAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Account) => {
      const result = await accountsClient.createAccount(body);
      if (result.status === 'ok' || result.status === 'created') {
        return result.data;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to create account',
        result.response,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: accountKeys.all });
    },
  });
}

export function useDeleteAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (accountId: string) => {
      const result = await accountsClient.deleteAccountById(accountId);
      if (result.status === 'noContent' || result.status === 'ok') {
        return;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to delete account',
        result.response,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: accountKeys.all });
    },
  });
}
