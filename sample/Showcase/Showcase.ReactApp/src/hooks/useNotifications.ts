import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { notificationsClient } from '../config/api';
import { ApiError } from '../api/errors/ApiError';
import type { CreateSubscriptionRequest } from '../api/models/CreateSubscriptionRequest';

const subscriptionKeys = {
  all: ['subscriptions'] as const,
  list: () => [...subscriptionKeys.all, 'list'] as const,
};

export function useSubscriptionsList() {
  return useQuery({
    queryKey: subscriptionKeys.list(),
    queryFn: async () => {
      const result = await notificationsClient.listSubscriptions();
      if (result.status === 'ok') {
        return result.data;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to list subscriptions',
        result.response,
      );
    },
    enabled: false,
  });
}

export function useCreateSubscription() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: CreateSubscriptionRequest) => {
      const result = await notificationsClient.createSubscription(body);
      if (result.status === 'ok' || result.status === 'created') {
        return result.data;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to create subscription',
        result.response,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: subscriptionKeys.all });
    },
  });
}

export function useDeleteSubscription() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (subscriptionId: string) => {
      const result = await notificationsClient.deleteSubscription(subscriptionId);
      if (result.status === 'noContent' || result.status === 'ok') {
        return;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to delete subscription',
        result.response,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: subscriptionKeys.all });
    },
  });
}
