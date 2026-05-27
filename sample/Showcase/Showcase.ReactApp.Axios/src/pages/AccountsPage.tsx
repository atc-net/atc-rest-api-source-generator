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
import { useAccountsList, useCreateAccount, useDeleteAccount } from '../hooks/useAccounts';
import { ConfirmDialog } from '../components/ConfirmDialog';
import type { Account } from '../api/models/Account';

export default function AccountsPage() {
  const { data: accounts, isLoading, refetch, error } = useAccountsList();
  const createAccount = useCreateAccount();
  const deleteAccount = useDeleteAccount();

  const [name, setName] = useState('');
  const [tag, setTag] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const [errorMsg, setErrorMsg] = useState('');

  const [deleteTarget, setDeleteTarget] = useState<Account | null>(null);

  const handleCreate = async () => {
    if (!name.trim()) return;
    setSuccessMsg('');
    setErrorMsg('');
    try {
      await createAccount.mutateAsync({ id: 0, name: name.trim(), tag: tag.trim() || undefined });
      setName('');
      setTag('');
      setSuccessMsg('Account created successfully.');
      refetch();
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to create account.');
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setSuccessMsg('');
    setErrorMsg('');
    try {
      await deleteAccount.mutateAsync(String(deleteTarget.id));
      setSuccessMsg('Account deleted successfully.');
      setDeleteTarget(null);
      refetch();
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to delete account.');
      setDeleteTarget(null);
    }
  };

  const accountList = Array.isArray(accounts) ? accounts : [];

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Accounts
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
          Create Account
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
            disabled={createAccount.isPending || !name.trim()}
          >
            {createAccount.isPending ? <CircularProgress size={20} /> : 'Create'}
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
        {isLoading ? 'Loading...' : 'Load Accounts'}
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
            {accountList.map((account) => (
              <TableRow key={account.id}>
                <TableCell>{account.id}</TableCell>
                <TableCell>{account.name}</TableCell>
                <TableCell>{account.tag ?? ''}</TableCell>
                <TableCell align="right">
                  <IconButton
                    color="error"
                    onClick={() => setDeleteTarget(account)}
                    disabled={deleteAccount.isPending}
                  >
                    <DeleteIcon />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
            {accountList.length === 0 && !isLoading && (
              <TableRow>
                <TableCell colSpan={4} align="center">
                  No accounts loaded. Click "Load Accounts" to fetch data.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete Account"
        message={`Are you sure you want to delete account "${deleteTarget?.name}"?`}
        confirmText="Delete"
        confirmColor="error"
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </Box>
  );
}
