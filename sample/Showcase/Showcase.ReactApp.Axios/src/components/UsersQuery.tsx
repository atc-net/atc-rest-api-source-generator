import { useState } from 'react';
import { Button, Paper, TextField, Typography } from '@mui/material';
import { useQueryUsers } from '../api/hooks/useUsers';
import type { UserQuery } from '../api/models/UserQuery';

/**
 * Looks users up by a set of ids with the OpenAPI 3.2 QUERY method (`queryUsers`): the ids travel in the request body,
 * so a set too large for a URL is still one request. Unknown ids are skipped.
 */
export default function UsersQuery() {
  const [idsText, setIdsText] = useState('');
  const [query, setQuery] = useState<UserQuery | null>(null);
  const { data: users, error, isFetching } = useQueryUsers(query ?? { ids: [] }, { enabled: query !== null });

  const runQuery = () => {
    const ids = idsText.split(/[\s,]+/).filter(Boolean);
    setQuery(ids.length > 0 ? { ids } : null);
  };

  return (
    <Paper sx={{ p: 3, mt: 3 }}>
      <Typography variant="h6" gutterBottom>
        Query users by ids
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        <code>QUERY /users</code> sends the ids in the request body - a safe, idempotent read like a GET, without a URL
        to outgrow.
      </Typography>
      <TextField
        label="User ids"
        helperText="One id per line (or separated by commas)"
        multiline
        minRows={3}
        fullWidth
        value={idsText}
        onChange={(e) => setIdsText(e.target.value)}
        slotProps={{ htmlInput: { 'data-testid': 'users-query-ids' } }}
      />
      <Button variant="contained" sx={{ mt: 1 }} disabled={isFetching} onClick={runQuery} data-testid="users-query">
        Query
      </Button>
      {query !== null && !isFetching && (
        <Typography variant="body2" sx={{ mt: 2 }} data-testid="users-query-status">
          {error ? `Query failed: ${error.message}` : `Found ${users?.length ?? 0} of ${query.ids.length} users`}
        </Typography>
      )}
      {query !== null &&
        !isFetching &&
        users?.map((user) => (
          <Typography key={user.id} variant="body2" data-testid="users-query-row">
            {`${user.firstName} ${user.lastName} · ${user.email}`}
          </Typography>
        ))}
    </Paper>
  );
}
