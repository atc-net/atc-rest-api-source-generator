import { useEffect } from 'react';
import {
  Box,
  Typography,
  TextField,
  Button,
  Paper,
  Grid2,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormControlLabel,
  Switch,
  CircularProgress,
  Alert,
  Card,
  CardContent,
  Avatar,
  Chip,
  Divider,
} from '@mui/material';
import { useNavigate, useParams } from 'react-router';
import { useForm, Controller } from 'react-hook-form';
import { useUser, useCreateUser, useUpdateUser } from '../hooks/useUsers';
import type { CreateUserRequest } from '../api/models/CreateUserRequest';
import type { UserRole } from '../api/enums/UserRole';

const userRoles: UserRole[] = ['Admin', 'Manager', 'Employee', 'Guest'];

interface UserFormData {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  dateOfBirth: string;
  role: UserRole;
  isActive: boolean;
  bio: string;
  avatarUrl: string;
  website: string;
  street: string;
  city: string;
  state: string;
  postalCode: string;
  country: string;
}

const defaultValues: UserFormData = {
  firstName: '',
  lastName: '',
  email: '',
  phone: '',
  dateOfBirth: '',
  role: 'Employee',
  isActive: true,
  bio: '',
  avatarUrl: '',
  website: '',
  street: '',
  city: '',
  state: '',
  postalCode: '',
  country: '',
};

export default function UserFormPage() {
  const { userId } = useParams<{ userId: string }>();
  const navigate = useNavigate();
  const isEditMode = Boolean(userId);

  const { data: existingUser, isLoading: isLoadingUser } = useUser(userId ?? '');
  const createUser = useCreateUser();
  const updateUser = useUpdateUser(userId ?? '');

  const {
    control,
    handleSubmit,
    reset,
    watch,
    formState: { errors },
  } = useForm<UserFormData>({ defaultValues });

  const watchedValues = watch();

  useEffect(() => {
    if (isEditMode && existingUser) {
      reset({
        firstName: existingUser.firstName ?? '',
        lastName: existingUser.lastName ?? '',
        email: existingUser.email ?? '',
        phone: existingUser.phone ?? '',
        dateOfBirth: existingUser.dateOfBirth ?? '',
        role: existingUser.role ?? 'Employee',
        isActive: existingUser.isActive ?? true,
        bio: existingUser.bio ?? '',
        avatarUrl: existingUser.avatarUrl ?? '',
        website: existingUser.website ?? '',
        street: existingUser.address?.street ?? '',
        city: existingUser.address?.city ?? '',
        state: existingUser.address?.state ?? '',
        postalCode: existingUser.address?.postalCode ?? '',
        country: existingUser.address?.country ?? '',
      });
    }
  }, [isEditMode, existingUser, reset]);

  const onSubmit = async (data: UserFormData) => {
    const body: CreateUserRequest = {
      firstName: data.firstName,
      lastName: data.lastName,
      email: data.email,
      phone: data.phone || null,
      dateOfBirth: data.dateOfBirth,
      role: data.role,
      isActive: data.isActive,
      bio: data.bio || null,
      avatarUrl: data.avatarUrl || null,
      website: data.website || null,
      address: {
        street: data.street,
        city: data.city,
        state: data.state || null,
        postalCode: data.postalCode,
        country: data.country,
      },
    };

    try {
      if (isEditMode) {
        await updateUser.mutateAsync(body);
        navigate(`/users/${userId}`);
      } else {
        const result = await createUser.mutateAsync(body);
        const newId = (result as { id?: string })?.id;
        navigate(newId ? `/users/${newId}` : '/users');
      }
    } catch {
      // Error is shown by mutation state
    }
  };

  const isPending = createUser.isPending || updateUser.isPending;
  const mutationError = createUser.error || updateUser.error;

  if (isEditMode && isLoadingUser) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        {isEditMode ? 'Edit User' : 'Create User'}
      </Typography>

      {mutationError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {mutationError.message}
        </Alert>
      )}

      <Grid2 container spacing={3}>
        <Grid2 size={{ xs: 12, md: 8 }}>
          <Paper sx={{ p: 3 }}>
            <form onSubmit={handleSubmit(onSubmit)}>
              <Typography variant="h6" gutterBottom>
                Personal Information
              </Typography>
              <Grid2 container spacing={2}>
                <Grid2 size={{ xs: 12, sm: 6 }}>
                  <Controller
                    name="firstName"
                    control={control}
                    rules={{ required: 'First name is required' }}
                    render={({ field }) => (
                      <TextField
                        {...field}
                        label="First Name"
                        fullWidth
                        error={Boolean(errors.firstName)}
                        helperText={errors.firstName?.message}
                      />
                    )}
                  />
                </Grid2>
                <Grid2 size={{ xs: 12, sm: 6 }}>
                  <Controller
                    name="lastName"
                    control={control}
                    rules={{ required: 'Last name is required' }}
                    render={({ field }) => (
                      <TextField
                        {...field}
                        label="Last Name"
                        fullWidth
                        error={Boolean(errors.lastName)}
                        helperText={errors.lastName?.message}
                      />
                    )}
                  />
                </Grid2>
                <Grid2 size={{ xs: 12, sm: 6 }}>
                  <Controller
                    name="email"
                    control={control}
                    rules={{ required: 'Email is required' }}
                    render={({ field }) => (
                      <TextField
                        {...field}
                        label="Email"
                        type="email"
                        fullWidth
                        error={Boolean(errors.email)}
                        helperText={errors.email?.message}
                      />
                    )}
                  />
                </Grid2>
                <Grid2 size={{ xs: 12, sm: 6 }}>
                  <Controller
                    name="phone"
                    control={control}
                    render={({ field }) => <TextField {...field} label="Phone" fullWidth />}
                  />
                </Grid2>
                <Grid2 size={{ xs: 12, sm: 6 }}>
                  <Controller
                    name="dateOfBirth"
                    control={control}
                    rules={{ required: 'Date of birth is required' }}
                    render={({ field }) => (
                      <TextField
                        {...field}
                        label="Date of Birth"
                        type="date"
                        fullWidth
                        slotProps={{ inputLabel: { shrink: true } }}
                        error={Boolean(errors.dateOfBirth)}
                        helperText={errors.dateOfBirth?.message}
                      />
                    )}
                  />
                </Grid2>
                <Grid2 size={{ xs: 12, sm: 6 }}>
                  <Controller
                    name="role"
                    control={control}
                    render={({ field }) => (
                      <FormControl fullWidth>
                        <InputLabel>Role</InputLabel>
                        <Select {...field} label="Role">
                          {userRoles.map((r) => (
                            <MenuItem key={r} value={r}>
                              {r}
                            </MenuItem>
                          ))}
                        </Select>
                      </FormControl>
                    )}
                  />
                </Grid2>
                <Grid2 size={{ xs: 12 }}>
                  <Controller
                    name="isActive"
                    control={control}
                    render={({ field }) => (
                      <FormControlLabel
                        control={<Switch checked={field.value} onChange={field.onChange} />}
                        label="Active"
                      />
                    )}
                  />
                </Grid2>
                <Grid2 size={{ xs: 12 }}>
                  <Controller
                    name="bio"
                    control={control}
                    render={({ field }) => (
                      <TextField {...field} label="Bio" multiline rows={3} fullWidth />
                    )}
                  />
                </Grid2>
                <Grid2 size={{ xs: 12, sm: 6 }}>
                  <Controller
                    name="avatarUrl"
                    control={control}
                    render={({ field }) => <TextField {...field} label="Avatar URL" fullWidth />}
                  />
                </Grid2>
                <Grid2 size={{ xs: 12, sm: 6 }}>
                  <Controller
                    name="website"
                    control={control}
                    render={({ field }) => <TextField {...field} label="Website URL" fullWidth />}
                  />
                </Grid2>
              </Grid2>

              <Divider sx={{ my: 3 }} />

              <Typography variant="h6" gutterBottom>
                Address
              </Typography>
              <Grid2 container spacing={2}>
                <Grid2 size={{ xs: 12 }}>
                  <Controller
                    name="street"
                    control={control}
                    rules={{ required: 'Street is required' }}
                    render={({ field }) => (
                      <TextField
                        {...field}
                        label="Street"
                        fullWidth
                        error={Boolean(errors.street)}
                        helperText={errors.street?.message}
                      />
                    )}
                  />
                </Grid2>
                <Grid2 size={{ xs: 12, sm: 6 }}>
                  <Controller
                    name="city"
                    control={control}
                    rules={{ required: 'City is required' }}
                    render={({ field }) => (
                      <TextField
                        {...field}
                        label="City"
                        fullWidth
                        error={Boolean(errors.city)}
                        helperText={errors.city?.message}
                      />
                    )}
                  />
                </Grid2>
                <Grid2 size={{ xs: 12, sm: 6 }}>
                  <Controller
                    name="state"
                    control={control}
                    render={({ field }) => <TextField {...field} label="State" fullWidth />}
                  />
                </Grid2>
                <Grid2 size={{ xs: 12, sm: 6 }}>
                  <Controller
                    name="postalCode"
                    control={control}
                    rules={{ required: 'Postal code is required' }}
                    render={({ field }) => (
                      <TextField
                        {...field}
                        label="Postal Code"
                        fullWidth
                        error={Boolean(errors.postalCode)}
                        helperText={errors.postalCode?.message}
                      />
                    )}
                  />
                </Grid2>
                <Grid2 size={{ xs: 12, sm: 6 }}>
                  <Controller
                    name="country"
                    control={control}
                    rules={{ required: 'Country is required' }}
                    render={({ field }) => (
                      <TextField
                        {...field}
                        label="Country"
                        fullWidth
                        error={Boolean(errors.country)}
                        helperText={errors.country?.message}
                      />
                    )}
                  />
                </Grid2>
              </Grid2>

              <Box sx={{ mt: 3, display: 'flex', gap: 2 }}>
                <Button type="submit" variant="contained" disabled={isPending}>
                  {isPending ? <CircularProgress size={20} /> : isEditMode ? 'Update' : 'Create'}
                </Button>
                <Button variant="outlined" onClick={() => navigate('/users')}>
                  Cancel
                </Button>
              </Box>
            </form>
          </Paper>
        </Grid2>

        <Grid2 size={{ xs: 12, md: 4 }}>
          <Card>
            <CardContent sx={{ textAlign: 'center' }}>
              <Typography variant="h6" gutterBottom>
                Preview
              </Typography>
              <Avatar
                src={watchedValues.avatarUrl || undefined}
                sx={{ width: 80, height: 80, mx: 'auto', mb: 2, fontSize: 32 }}
              >
                {watchedValues.firstName?.[0]}
                {watchedValues.lastName?.[0]}
              </Avatar>
              <Typography variant="h6">
                {watchedValues.firstName} {watchedValues.lastName}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {watchedValues.email}
              </Typography>
              {watchedValues.role && (
                <Chip label={watchedValues.role} size="small" sx={{ mt: 1 }} />
              )}
              {watchedValues.bio && (
                <Typography variant="body2" sx={{ mt: 2, fontStyle: 'italic' }}>
                  {watchedValues.bio}
                </Typography>
              )}
              {watchedValues.city && watchedValues.country && (
                <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                  {watchedValues.city}, {watchedValues.country}
                </Typography>
              )}
            </CardContent>
          </Card>
        </Grid2>
      </Grid2>
    </Box>
  );
}
