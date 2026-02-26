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
  Chip,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  OutlinedInput,
} from '@mui/material';
import type { SelectChangeEvent } from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import RefreshIcon from '@mui/icons-material/Refresh';
import {
  useSubscriptionsList,
  useCreateSubscription,
  useDeleteSubscription,
} from '../hooks/useNotifications';
import { ConfirmDialog } from '../components/ConfirmDialog';
import type { Subscription } from '../api/models/Subscription';
import type { NotificationType } from '../api/enums/NotificationType';

const notificationTypes: NotificationType[] = ['System', 'User', 'Data', 'Alert', 'Metric'];

export default function NotificationSubscriptionsPage() {
  const { data: subscriptions, isLoading, refetch, error } = useSubscriptionsList();
  const createSubscription = useCreateSubscription();
  const deleteSubscription = useDeleteSubscription();

  const [name, setName] = useState('');
  const [selectedTopics, setSelectedTopics] = useState<NotificationType[]>([]);
  const [successMsg, setSuccessMsg] = useState('');
  const [errorMsg, setErrorMsg] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<Subscription | null>(null);

  const handleTopicsChange = (event: SelectChangeEvent<NotificationType[]>) => {
    const value = event.target.value;
    setSelectedTopics(typeof value === 'string' ? (value.split(',') as NotificationType[]) : value);
  };

  const handleCreate = async () => {
    if (selectedTopics.length === 0) return;
    setSuccessMsg('');
    setErrorMsg('');
    try {
      await createSubscription.mutateAsync({
        name: name.trim() || null,
        topics: selectedTopics,
      });
      setName('');
      setSelectedTopics([]);
      setSuccessMsg('Subscription created successfully.');
      refetch();
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to create subscription.');
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setSuccessMsg('');
    setErrorMsg('');
    try {
      await deleteSubscription.mutateAsync(deleteTarget.id);
      setSuccessMsg('Subscription deleted successfully.');
      setDeleteTarget(null);
      refetch();
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to delete subscription.');
      setDeleteTarget(null);
    }
  };

  const subList = Array.isArray(subscriptions) ? subscriptions : [];

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Notification Subscriptions
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
          Create Subscription
        </Typography>
        <Stack direction="row" spacing={2} alignItems="center" flexWrap="wrap">
          <TextField
            label="Name (optional)"
            size="small"
            value={name}
            onChange={(e) => setName(e.target.value)}
            sx={{ minWidth: 200 }}
          />
          <FormControl size="small" sx={{ minWidth: 250 }}>
            <InputLabel>Topics</InputLabel>
            <Select
              multiple
              value={selectedTopics}
              onChange={handleTopicsChange}
              input={<OutlinedInput label="Topics" />}
              renderValue={(selected) => (
                <Stack direction="row" spacing={0.5} flexWrap="wrap">
                  {selected.map((t) => (
                    <Chip key={t} label={t} size="small" />
                  ))}
                </Stack>
              )}
            >
              {notificationTypes.map((t) => (
                <MenuItem key={t} value={t}>
                  {t}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <Button
            variant="contained"
            onClick={handleCreate}
            disabled={createSubscription.isPending || selectedTopics.length === 0}
          >
            {createSubscription.isPending ? <CircularProgress size={20} /> : 'Create'}
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
        {isLoading ? 'Loading...' : 'Load Subscriptions'}
      </Button>

      {isLoading && <CircularProgress sx={{ display: 'block', my: 2 }} />}

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>ID</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Topics</TableCell>
              <TableCell>Active</TableCell>
              <TableCell>Created</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {subList.map((sub) => (
              <TableRow key={sub.id}>
                <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.85rem' }}>
                  {sub.id.substring(0, 8)}...
                </TableCell>
                <TableCell>{sub.name ?? '(unnamed)'}</TableCell>
                <TableCell>
                  <Stack direction="row" spacing={0.5} flexWrap="wrap">
                    {sub.topics.map((t) => (
                      <Chip key={t} label={t} size="small" variant="outlined" />
                    ))}
                  </Stack>
                </TableCell>
                <TableCell>
                  <Chip
                    label={sub.isActive ? 'Active' : 'Inactive'}
                    size="small"
                    color={sub.isActive ? 'success' : 'default'}
                    variant="outlined"
                  />
                </TableCell>
                <TableCell>{new Date(sub.createdAt).toLocaleDateString()}</TableCell>
                <TableCell align="right">
                  <IconButton
                    color="error"
                    onClick={() => setDeleteTarget(sub)}
                    disabled={deleteSubscription.isPending}
                  >
                    <DeleteIcon />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
            {subList.length === 0 && !isLoading && (
              <TableRow>
                <TableCell colSpan={6} align="center">
                  No subscriptions loaded. Click "Load Subscriptions" to fetch data.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete Subscription"
        message={`Are you sure you want to delete subscription "${deleteTarget?.name ?? deleteTarget?.id}"?`}
        confirmText="Delete"
        confirmColor="error"
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </Box>
  );
}
