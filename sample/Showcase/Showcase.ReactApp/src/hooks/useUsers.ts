import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { usersClient } from '../config/api';
import { ApiError } from '../api/errors/ApiError';
import type { CreateUserRequest } from '../api/models/CreateUserRequest';
import type { UpdateUserRequest } from '../api/models/UpdateUserRequest';

interface UserListParams {
  search?: string;
  country?: string;
  role?: string;
  isActive?: boolean;
  limit?: number;
}

const userKeys = {
  all: ['users'] as const,
  list: (params?: UserListParams) => [...userKeys.all, 'list', params] as const,
  detail: (userId: string) => [...userKeys.all, 'detail', userId] as const,
};

export function useUsersList(params?: UserListParams) {
  return useQuery({
    queryKey: userKeys.list(params),
    queryFn: async () => {
      const result = await usersClient.listUsers(params);
      if (result.status === 'ok') {
        return result.data;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to list users',
        result.response,
      );
    },
    enabled: false,
  });
}

export function useUser(userId: string) {
  return useQuery({
    queryKey: userKeys.detail(userId),
    queryFn: async () => {
      const result = await usersClient.getUserById(userId);
      if (result.status === 'ok') {
        return result.data;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to get user',
        result.response,
      );
    },
    enabled: !!userId,
  });
}

export function useCreateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: CreateUserRequest) => {
      const result = await usersClient.createUser(body);
      if (result.status === 'ok' || result.status === 'created') {
        return result.data;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to create user',
        result.response,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: userKeys.all });
    },
  });
}

export function useUpdateUser(userId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: UpdateUserRequest) => {
      const result = await usersClient.updateUserById(userId, body);
      if (result.status === 'ok' || result.status === 'created') {
        return result.data;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to update user',
        result.response,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: userKeys.all });
    },
  });
}

export function useDeleteUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (userId: string) => {
      const result = await usersClient.deleteUserById(userId);
      if (result.status === 'noContent' || result.status === 'ok') {
        return;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to delete user',
        result.response,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: userKeys.all });
    },
  });
}
