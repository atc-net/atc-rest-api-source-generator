import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { tasksClient } from '../config/api';
import { ApiError } from '../api/errors/ApiError';
import type { Task } from '../api/models/Task';

const taskKeys = {
  all: ['tasks'] as const,
  list: (limit?: number) => [...taskKeys.all, 'list', { limit }] as const,
};

export function useTasksList(limit?: number) {
  return useQuery({
    queryKey: taskKeys.list(limit),
    queryFn: async () => {
      const result = await tasksClient.listTasks({ limit });
      if (result.status === 'ok') {
        return result.data;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to list tasks',
        result.response,
      );
    },
    enabled: false,
  });
}

export function useCreateTask() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: Task) => {
      const result = await tasksClient.createTask(body);
      if (result.status === 'ok' || result.status === 'created') {
        return result.data;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to create task',
        result.response,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: taskKeys.all });
    },
  });
}

export function useDeleteTask() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (taskId: string) => {
      const result = await tasksClient.deleteTaskById(taskId);
      if (result.status === 'noContent' || result.status === 'ok') {
        return;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to delete task',
        result.response,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: taskKeys.all });
    },
  });
}
