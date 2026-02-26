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
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Chip,
  Avatar,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import SearchIcon from '@mui/icons-material/Search';
import { useNavigate } from 'react-router';
import { useUsersList, useDeleteUser } from '../hooks/useUsers';
import { ConfirmDialog } from '../components/ConfirmDialog';
import type { User } from '../api/models/User';

const roles = ['', 'Admin', 'Manager', 'Employee', 'Guest'] as const;
const activeOptions = [
  { label: 'Any', value: '' },
  { label: 'Active', value: 'true' },
  { label: 'Inactive', value: 'false' },
];

export default function UsersPage() {
  const navigate = useNavigate();

  const [search, setSearch] = useState('');
  const [country, setCountry] = useState('');
  const [role, setRole] = useState('');
  const [activeStatus, setActiveStatus] = useState('');

  const params = {
    search: search || undefined,
    country: country || undefined,
    role: role || undefined,
    isActive: activeStatus === '' ? undefined : activeStatus === 'true',
  };

  const { data: users, isLoading, refetch, error } = useUsersList(params);
  const deleteUser = useDeleteUser();

  const [successMsg, setSuccessMsg] = useState('');
  const [errorMsg, setErrorMsg] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<User | null>(null);

  const handleSearch = () => {
    refetch();
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setSuccessMsg('');
    setErrorMsg('');
    try {
      await deleteUser.mutateAsync(deleteTarget.id);
      setSuccessMsg('User deleted successfully.');
      setDeleteTarget(null);
      refetch();
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to delete user.');
      setDeleteTarget(null);
    }
  };

  const userList = Array.isArray(users) ? users : [];

  const roleColor = (r: string): 'primary' | 'secondary' | 'success' | 'default' => {
    switch (r) {
      case 'Admin':
        return 'primary';
      case 'Manager':
        return 'secondary';
      case 'Employee':
        return 'success';
      default:
        return 'default';
    }
  };

  return (
    <Box>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 3 }}>
        <Typography variant="h4">Users</Typography>
        <Button variant="contained" onClick={() => navigate('/users/create')}>
          Create User
        </Button>
      </Stack>

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
          Filter
        </Typography>
        <Stack direction="row" spacing={2} alignItems="center" flexWrap="wrap">
          <TextField
            label="Search"
            size="small"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            sx={{ minWidth: 200 }}
          />
          <TextField
            label="Country"
            size="small"
            value={country}
            onChange={(e) => setCountry(e.target.value)}
            sx={{ minWidth: 140 }}
          />
          <FormControl size="small" sx={{ minWidth: 130 }}>
            <InputLabel>Role</InputLabel>
            <Select value={role} label="Role" onChange={(e) => setRole(e.target.value)}>
              {roles.map((r) => (
                <MenuItem key={r} value={r}>
                  {r || 'Any'}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <FormControl size="small" sx={{ minWidth: 130 }}>
            <InputLabel>Active</InputLabel>
            <Select
              value={activeStatus}
              label="Active"
              onChange={(e) => setActiveStatus(e.target.value)}
            >
              {activeOptions.map((opt) => (
                <MenuItem key={opt.value} value={opt.value}>
                  {opt.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <Button
            variant="contained"
            startIcon={<SearchIcon />}
            onClick={handleSearch}
            disabled={isLoading}
          >
            Search
          </Button>
        </Stack>
      </Paper>

      {isLoading && <CircularProgress sx={{ display: 'block', my: 2 }} />}

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Avatar</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Email</TableCell>
              <TableCell>Role</TableCell>
              <TableCell>Country</TableCell>
              <TableCell>Active</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {userList.map((user) => (
              <TableRow
                key={user.id}
                hover
                sx={{ cursor: 'pointer' }}
                onClick={() => navigate(`/users/${user.id}`)}
              >
                <TableCell>
                  <Avatar src={user.avatarUrl ?? undefined} sx={{ width: 32, height: 32 }}>
                    {user.firstName?.[0]}
                    {user.lastName?.[0]}
                  </Avatar>
                </TableCell>
                <TableCell>
                  {user.firstName} {user.lastName}
                </TableCell>
                <TableCell>{user.email}</TableCell>
                <TableCell>
                  <Chip label={user.role} size="small" color={roleColor(user.role)} />
                </TableCell>
                <TableCell>{user.address?.country ?? ''}</TableCell>
                <TableCell>
                  <Chip
                    label={user.isActive ? 'Active' : 'Inactive'}
                    size="small"
                    color={user.isActive ? 'success' : 'default'}
                    variant="outlined"
                  />
                </TableCell>
                <TableCell align="right">
                  <IconButton
                    color="error"
                    onClick={(e) => {
                      e.stopPropagation();
                      setDeleteTarget(user);
                    }}
                    disabled={deleteUser.isPending}
                  >
                    <DeleteIcon />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
            {userList.length === 0 && !isLoading && (
              <TableRow>
                <TableCell colSpan={7} align="center">
                  No users found. Click "Search" to load users.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <ConfirmDialog
        open={deleteTarget !== null}
        title="Delete User"
        message={`Are you sure you want to delete user "${deleteTarget?.firstName} ${deleteTarget?.lastName}"?`}
        confirmText="Delete"
        confirmColor="error"
        onConfirm={handleDelete}
        onCancel={() => setDeleteTarget(null)}
      />
    </Box>
  );
}
