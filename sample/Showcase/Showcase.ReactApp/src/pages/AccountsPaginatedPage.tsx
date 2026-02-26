import { useState } from 'react';
import {
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Button,
  TextField,
  CircularProgress,
  Alert,
  Stack,
} from '@mui/material';
import { useAccountsPaginated } from '../hooks/useAccountsPaginated';

export default function AccountsPaginatedPage() {
  const [pageSize, setPageSize] = useState(10);
  const [queryString, setQueryString] = useState('');
  const [continuation, setContinuation] = useState<string | undefined>(undefined);
  const [pageHistory, setPageHistory] = useState<string[]>([]);

  const { data, isLoading, refetch, error } = useAccountsPaginated({
    pageSize,
    queryString: queryString || undefined,
    continuation,
  });

  const handleLoad = () => {
    setContinuation(undefined);
    setPageHistory([]);
    refetch();
  };

  const handleNextPage = () => {
    if (data?.continuation) {
      setPageHistory((prev) => [...prev, continuation ?? '']);
      setContinuation(data.continuation);
      setTimeout(() => refetch(), 0);
    }
  };

  const handlePreviousPage = () => {
    if (pageHistory.length > 0) {
      const prev = [...pageHistory];
      const last = prev.pop()!;
      setPageHistory(prev);
      setContinuation(last || undefined);
      setTimeout(() => refetch(), 0);
    }
  };

  const handleFirstPage = () => {
    setContinuation(undefined);
    setPageHistory([]);
    setTimeout(() => refetch(), 0);
  };

  const results = Array.isArray(data?.results) ? data.results : [];

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Accounts (Paginated)
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error.message}
        </Alert>
      )}

      <Paper sx={{ p: 2, mb: 3 }}>
        <Stack direction="row" spacing={2} alignItems="center">
          <TextField
            label="Page Size"
            type="number"
            size="small"
            value={pageSize}
            onChange={(e) => setPageSize(Number(e.target.value) || 10)}
            sx={{ width: 120 }}
          />
          <TextField
            label="Query String"
            size="small"
            value={queryString}
            onChange={(e) => setQueryString(e.target.value)}
          />
          <Button variant="contained" onClick={handleLoad} disabled={isLoading}>
            Load
          </Button>
        </Stack>
      </Paper>

      {data && (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          Showing {results.length} of {data.totalCount ?? '?'} total results
          {data.pageIndex !== undefined && ` | Page ${data.pageIndex}`}
        </Typography>
      )}

      {isLoading && <CircularProgress sx={{ display: 'block', my: 2 }} />}

      <TableContainer component={Paper} sx={{ mb: 2 }}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>ID</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Tag</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {results.map((item, idx) => {
              const row = item as Record<string, unknown>;
              return (
                <TableRow key={String(row.id ?? idx)}>
                  <TableCell>{String(row.id ?? '')}</TableCell>
                  <TableCell>{String(row.name ?? '')}</TableCell>
                  <TableCell>{String(row.tag ?? '')}</TableCell>
                </TableRow>
              );
            })}
            {results.length === 0 && !isLoading && (
              <TableRow>
                <TableCell colSpan={3} align="center">
                  No results. Click "Load" to fetch paginated data.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Stack direction="row" spacing={2}>
        <Button
          variant="outlined"
          onClick={handleFirstPage}
          disabled={isLoading || pageHistory.length === 0}
        >
          First Page
        </Button>
        <Button
          variant="outlined"
          onClick={handlePreviousPage}
          disabled={isLoading || pageHistory.length === 0}
        >
          Previous Page
        </Button>
        <Button
          variant="outlined"
          onClick={handleNextPage}
          disabled={isLoading || !data?.continuation}
        >
          Next Page
        </Button>
      </Stack>
    </Box>
  );
}
