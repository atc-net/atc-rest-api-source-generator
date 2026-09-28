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
import { ApiProvider } from '../api-swr/hooks/ApiProvider';
import { useListTasks, useCreateTask, useGetTaskById } from '../api-swr/hooks/useTasks';
import { apiBaseUrl, demoToken } from '../config/api';

/**
 * The generated SWR hooks (`--hooks Swr`, generated into `src/api-swr`) against the live API.
 * No manual refetch: a created task shows up because the mutation revalidates the segment's queries,
 * and a lookup of an unknown id shows the server's ApiError after a single request.
 */
export default function SwrTasksPage() {
  return (
    <ApiProvider baseUrl={apiBaseUrl + '/api/v1'} options={{ getAccessToken: () => demoToken }}>
      <SwrTasks />
    </ApiProvider>
  );
}

function SwrTasks() {
  const { data: tasks, isLoading, error } = useListTasks();
  const createTask = useCreateTask();

  const [name, setName] = useState('');
  const [lookupInput, setLookupInput] = useState('');
  const [lookupId, setLookupId] = useState('');

  // An empty id keeps the query idle: the hook makes no request until an id is looked up.
  const lookup = useGetTaskById(lookupId);

  const handleCreate = async () => {
    if (!name.trim()) return;
    await createTask.trigger({ id: 0, name: name.trim() });
    setName('');
  };

  const taskList = Array.isArray(tasks) ? tasks : [];

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Tasks (SWR hooks)
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error.message}
        </Alert>
      )}
      {createTask.error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {createTask.error.message}
        </Alert>
      )}

      <Paper sx={{ p: 2, mb: 3 }}>
        <Typography variant="h6" gutterBottom>
          Create Task
        </Typography>
        <Stack direction="row" spacing={2} alignItems="center">
          <TextField
            label="Name"
            size="small"
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
          <Button
            variant="contained"
            onClick={handleCreate}
            disabled={createTask.isMutating || !name.trim()}
          >
            {createTask.isMutating ? <CircularProgress size={20} /> : 'Create'}
          </Button>
        </Stack>
      </Paper>

      <Paper sx={{ p: 2, mb: 3 }}>
        <Typography variant="h6" gutterBottom>
          Look Up Task
        </Typography>
        <Stack direction="row" spacing={2} alignItems="center">
          <TextField
            label="Task Id"
            size="small"
            value={lookupInput}
            onChange={(e) => setLookupInput(e.target.value)}
          />
          <Button variant="outlined" onClick={() => setLookupId(lookupInput.trim())}>
            Look Up
          </Button>
        </Stack>
        {lookup.isLoading && <CircularProgress size={20} sx={{ mt: 2 }} />}
        {lookup.data && (
          <Alert severity="success" sx={{ mt: 2 }} data-testid="swr-lookup-result">
            {lookup.data.id}: {lookup.data.name}
          </Alert>
        )}
        {lookup.error && (
          <Alert severity="error" sx={{ mt: 2 }} data-testid="swr-lookup-error">
            {lookup.error.status}: {lookup.error.message}
          </Alert>
        )}
      </Paper>

      {isLoading && <CircularProgress sx={{ display: 'block', my: 2 }} />}

      <TableContainer component={Paper}>
        <Table data-testid="swr-tasks-table">
          <TableHead>
            <TableRow>
              <TableCell>ID</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Tag</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {taskList.map((task) => (
              <TableRow key={task.id}>
                <TableCell>{task.id}</TableCell>
                <TableCell>{task.name}</TableCell>
                <TableCell>{task.tag ?? ''}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
}
