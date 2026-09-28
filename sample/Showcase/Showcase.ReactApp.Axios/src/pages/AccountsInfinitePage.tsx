import { Box, Button, CircularProgress, Paper, Stack, Typography } from '@mui/material';
import type { Account } from '../api/models/Account';
import { useListPaginatedAccountsInfinite as useListPaginatedAccountsInfiniteQuery } from '../api/hooks/useAccounts';
import { ApiProvider as SwrApiProvider } from '../api-swr/hooks/ApiProvider';
import { useListPaginatedAccountsInfinite as useListPaginatedAccountsInfiniteSwr } from '../api-swr/hooks/useAccounts';
import { apiBaseUrl, demoToken } from '../config/api';

const pageSize = 50;

/**
 * The generated infinite hooks of a paginated read (listPaginatedAccounts) against the live API: each page
 * sends the previous page's `continuation` back through the operation's `continuation` query parameter,
 * and the hooks stop at the first page without one. React Query (useInfiniteQuery) and SWR (useSWRInfinite).
 */
export default function AccountsInfinitePage() {
  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Accounts (infinite)
      </Typography>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
        <ReactQueryAccounts />
        <SwrApiProvider baseUrl={apiBaseUrl + '/api/v1'} options={{ getAccessToken: () => demoToken }}>
          <SwrAccounts />
        </SwrApiProvider>
      </Stack>
    </Box>
  );
}

function ReactQueryAccounts() {
  const query = useListPaginatedAccountsInfiniteQuery({ pageSize });
  const pages = query.data?.pages ?? [];

  return (
    <InfiniteAccounts
      title="React Query (useInfiniteQuery)"
      testId="rq"
      pages={pages.map((page) => page.results ?? [])}
      loading={query.isLoading || query.isFetchingNextPage}
      hasMore={query.hasNextPage}
      error={query.error?.message}
      onLoadMore={() => void query.fetchNextPage()}
    />
  );
}

function SwrAccounts() {
  const infinite = useListPaginatedAccountsInfiniteSwr({ pageSize });
  const pages = infinite.data ?? [];
  const lastPage = pages.length > 0 ? pages[pages.length - 1] : undefined;

  return (
    <InfiniteAccounts
      title="SWR (useSWRInfinite)"
      testId="swr"
      pages={pages.map((page) => page.results ?? [])}
      loading={infinite.isLoading || (infinite.size > 0 && pages.length < infinite.size)}
      hasMore={lastPage !== undefined && !!lastPage.continuation}
      error={infinite.error?.message}
      onLoadMore={() => void infinite.setSize(infinite.size + 1)}
    />
  );
}

interface InfiniteAccountsProps {
  title: string;
  testId: string;
  pages: Account[][];
  loading: boolean;
  hasMore: boolean;
  error: string | undefined;
  onLoadMore: () => void;
}

function InfiniteAccounts({ title, testId, pages, loading, hasMore, error, onLoadMore }: InfiniteAccountsProps) {
  const accounts = pages.flat();

  return (
    <Paper sx={{ p: 2, flex: 1 }} data-testid={`${testId}-infinite`}>
      <Typography variant="h6" gutterBottom>
        {title}
      </Typography>
      <Typography data-testid={`${testId}-summary`}>
        {accounts.length} accounts in {pages.length} pages
      </Typography>
      {error && <Typography color="error">{error}</Typography>}
      <Stack direction="row" spacing={2} alignItems="center" sx={{ my: 2 }}>
        <Button variant="contained" onClick={onLoadMore} disabled={loading || !hasMore}>
          Load more
        </Button>
        {loading && <CircularProgress size={20} />}
        {!loading && !hasMore && pages.length > 0 && <Typography data-testid={`${testId}-done`}>All pages loaded</Typography>}
      </Stack>
      <Box component="ol" sx={{ maxHeight: 300, overflow: 'auto', m: 0 }}>
        {accounts.map((account) => (
          <li key={account.id}>{account.name}</li>
        ))}
      </Box>
    </Paper>
  );
}
