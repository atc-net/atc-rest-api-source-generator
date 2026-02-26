import { useState } from 'react';
import {
  Box,
  Typography,
  Paper,
  Grid2,
  Avatar,
  Chip,
  CircularProgress,
  Alert,
  Button,
  Divider,
  Stack,
  Link,
} from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { useNavigate, useParams } from 'react-router';
import { useUser, useDeleteUser } from '../hooks/useUsers';
import { ConfirmDialog } from '../components/ConfirmDialog';

export default function UserDetailsPage() {
  const { userId } = useParams<{ userId: string }>();
  const navigate = useNavigate();
  const { data: user, isLoading, error } = useUser(userId ?? '');
  const deleteUser = useDeleteUser();

  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  const handleDelete = async () => {
    if (!userId) return;
    try {
      await deleteUser.mutateAsync(userId);
      navigate('/users');
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to delete user.');
      setShowDeleteDialog(false);
    }
  };

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return (
      <Box>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error.message}
        </Alert>
        <Button startIcon={<ArrowBackIcon />} onClick={() => navigate('/users')}>
          Back to Users
        </Button>
      </Box>
    );
  }

  if (!user) {
    return (
      <Box>
        <Alert severity="warning">User not found.</Alert>
        <Button startIcon={<ArrowBackIcon />} onClick={() => navigate('/users')} sx={{ mt: 2 }}>
          Back to Users
        </Button>
      </Box>
    );
  }

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
      {errorMsg && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setErrorMsg('')}>
          {errorMsg}
        </Alert>
      )}

      <Stack direction="row" spacing={2} sx={{ mb: 3 }}>
        <Button startIcon={<ArrowBackIcon />} onClick={() => navigate('/users')}>
          Back to Users
        </Button>
        <Button
          variant="outlined"
          startIcon={<EditIcon />}
          onClick={() => navigate(`/users/${userId}/edit`)}
        >
          Edit
        </Button>
        <Button
          variant="outlined"
          color="error"
          startIcon={<DeleteIcon />}
          onClick={() => setShowDeleteDialog(true)}
        >
          Delete
        </Button>
      </Stack>

      <Grid2 container spacing={3}>
        <Grid2 size={{ xs: 12, md: 4 }}>
          <Paper sx={{ p: 3, textAlign: 'center' }}>
            <Avatar
              src={user.avatarUrl ?? undefined}
              sx={{ width: 100, height: 100, mx: 'auto', mb: 2, fontSize: 40 }}
            >
              {user.firstName?.[0]}
              {user.lastName?.[0]}
            </Avatar>
            <Typography variant="h5">
              {user.firstName} {user.lastName}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
              {user.email}
            </Typography>
            <Stack direction="row" spacing={1} justifyContent="center">
              <Chip label={user.role} color={roleColor(user.role)} size="small" />
              <Chip
                label={user.isActive ? 'Active' : 'Inactive'}
                color={user.isActive ? 'success' : 'default'}
                variant="outlined"
                size="small"
              />
            </Stack>
            {user.age !== undefined && (
              <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                Age: {user.age}
              </Typography>
            )}
          </Paper>
        </Grid2>

        <Grid2 size={{ xs: 12, md: 8 }}>
          <Paper sx={{ p: 3, mb: 3 }}>
            <Typography variant="h6" gutterBottom>
              Contact Information
            </Typography>
            <Grid2 container spacing={2}>
              <Grid2 size={{ xs: 12, sm: 6 }}>
                <Typography variant="body2" color="text.secondary">
                  Email
                </Typography>
                <Typography>{user.email}</Typography>
              </Grid2>
              <Grid2 size={{ xs: 12, sm: 6 }}>
                <Typography variant="body2" color="text.secondary">
                  Phone
                </Typography>
                <Typography>{user.phone ?? 'Not provided'}</Typography>
              </Grid2>
              <Grid2 size={{ xs: 12, sm: 6 }}>
                <Typography variant="body2" color="text.secondary">
                  Date of Birth
                </Typography>
                <Typography>{user.dateOfBirth}</Typography>
              </Grid2>
              <Grid2 size={{ xs: 12, sm: 6 }}>
                <Typography variant="body2" color="text.secondary">
                  Website
                </Typography>
                {user.website ? (
                  <Link href={user.website} target="_blank" rel="noopener noreferrer">
                    {user.website}
                  </Link>
                ) : (
                  <Typography>Not provided</Typography>
                )}
              </Grid2>
            </Grid2>
          </Paper>

          {user.bio && (
            <Paper sx={{ p: 3, mb: 3 }}>
              <Typography variant="h6" gutterBottom>
                Bio
              </Typography>
              <Typography variant="body1">{user.bio}</Typography>
            </Paper>
          )}

          <Paper sx={{ p: 3, mb: 3 }}>
            <Typography variant="h6" gutterBottom>
              Address
            </Typography>
            {user.address ? (
              <Box>
                <Typography>{user.address.street}</Typography>
                <Typography>
                  {user.address.city}
                  {user.address.state ? `, ${user.address.state}` : ''} {user.address.postalCode}
                </Typography>
                <Typography>
                  {user.address.country}
                  {user.address.countryCode ? ` (${user.address.countryCode})` : ''}
                </Typography>
                {user.address.latitude != null && user.address.longitude != null && (
                  <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                    Coordinates: {user.address.latitude}, {user.address.longitude}
                  </Typography>
                )}
              </Box>
            ) : (
              <Typography color="text.secondary">No address on file.</Typography>
            )}
          </Paper>

          {(user.createdAt || user.updatedAt) && (
            <Paper sx={{ p: 3 }}>
              <Typography variant="h6" gutterBottom>
                Metadata
              </Typography>
              <Divider sx={{ mb: 2 }} />
              {user.createdAt && (
                <Typography variant="body2" color="text.secondary">
                  Created: {new Date(user.createdAt).toLocaleString()}
                </Typography>
              )}
              {user.updatedAt && (
                <Typography variant="body2" color="text.secondary">
                  Updated: {new Date(user.updatedAt).toLocaleString()}
                </Typography>
              )}
            </Paper>
          )}
        </Grid2>
      </Grid2>

      <ConfirmDialog
        open={showDeleteDialog}
        title="Delete User"
        message={`Are you sure you want to delete "${user.firstName} ${user.lastName}"? This action cannot be undone.`}
        confirmText="Delete"
        confirmColor="error"
        onConfirm={handleDelete}
        onCancel={() => setShowDeleteDialog(false)}
      />
    </Box>
  );
}
