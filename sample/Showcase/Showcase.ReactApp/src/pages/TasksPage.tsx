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
  IconButton,
  Stack,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import RefreshIcon from '@mui/icons-material/Refresh';
import { useTasksList, useCreateTask, useDeleteTask } from '../hooks/useTasks';
import { ConfirmDialog } from '../components/ConfirmDialog';
import type { Task } from '../api/models/Task';

export default function TasksPage() {
  const { data: tasks, isLoading, refetch, error } = useTasksList();
  const createTask = useCreateTask();
  const deleteTask = useDeleteTask();

  const [name, setName] = useState('');
  const [tag, setTag] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const [errorMsg, setErrorMsg] = useState('');

  const [deleteTarget, setDeleteTarget] = useState<Task | null>(null);

  const handleCreate = async () => {
    if (!name.trim()) return;
    setSuccessMsg('');
    setErrorMsg('');
    try {
      await createTask.mutateAsync({ id: 0, name: name.trim(), tag: tag.trim() || undefined });
      setName('');
      setTag('');
      setSuccessMsg('Task created successfully.');
      refetch();
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to create task.');
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setSuccessMsg('');
    setErrorMsg('');
    try {
      await deleteTask.mutateAsync(String(deleteTarget.id));
      setSuccessMsg('Task deleted successfully.');
      setDeleteTarget(null);
      refetch();
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to delete task.');
      setDeleteTarget(null);
    }
  };

  const taskList = Array.isArray(tasks) ? tasks : [];

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Tasks
      </Typography>

      {successMsg && (
        <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccessMsg('')}>
          {successMsg}
        </Alert>
      )}
      {errorMsg && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setErrorMsg('')}>
          {errorMsg}
        </Alert>
      )}
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error.message}
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
          <TextField
            label="Tag"
            size="small"
            value={tag}
            onChange={(e) => setTag(e.target.value)}
          />
          <Button
            variant="contained"
            onClick={handleCreate}
            disabled={createTask.isPending || !name.trim()}
          >
            {createTask.isPending ? <CircularProgress size={20} /> : 'Create'}
          </Button>
        </Stack>
      </Paper>

      <Button
        variant="outlined"
        startIcon={<RefreshIcon />}
        onClick={() => refetch()}
        disabled={isLoading}
        sx={{ mb: 2 }}
      >
        {isLoading ? 'Loading...' : 'Load Tasks'}
      </Button>

      {isLoading && <CircularProgress sx={{ display: 'block', my: 2 }} />}

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>ID</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Tag</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {taskList.map((task) => (
              <TableRow key={task.id}>
                <TableCell>{task.id}</TableCell>
                <TableCell>{task.name}</TableCell>
                <TableCell>{task.tag ?? ''}</TableCell>
                <TableCell align="right">
                  <IconButton
                    color="error"
                    onClick={() => setDeleteTarget(task)}
                    disabled={deleteTask.isPending}
                  >
                    <DeleteIcon />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
            {taskList.length === 0 && !isLoading && (
              <TableRow>
                <TableCell colSpan={4} align="center">
                  No tasks loaded. Click "Load Tasks" to fetch data.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete Task"
        message={`Are you sure you want to delete task "${deleteTarget?.name}"?`}
        confirmText="Delete"
        confirmColor="error"
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </Box>
  );
}
