import { useQuery } from '@tanstack/react-query';
import { accountsClient } from '../config/api';
import { ApiError } from '../api/errors/ApiError';

interface PaginatedParams {
  pageSize?: number;
  pageIndex?: number;
  queryString?: string;
  continuation?: string;
}

const accountPaginatedKeys = {
  all: ['accounts', 'paginated'] as const,
  page: (params: PaginatedParams) => [...accountPaginatedKeys.all, params] as const,
};

export function useAccountsPaginated(params: PaginatedParams) {
  return useQuery({
    queryKey: accountPaginatedKeys.page(params),
    queryFn: async () => {
      const result = await accountsClient.listPaginatedAccounts(params);
      if (result.status === 'ok') {
        return result.data;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to list paginated accounts',
        result.response,
      );
    },
    enabled: false,
  });
}
