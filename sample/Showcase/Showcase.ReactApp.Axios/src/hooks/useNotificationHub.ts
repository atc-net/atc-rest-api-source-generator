import { useCallback, useEffect, useRef, useState } from 'react';
import { useNotificationsHub, type NotificationsHubCallbacks } from '../api/hubs';

/**
 * The notifications hub with its topic subscription: the connection, its events and the hub's methods are the
 * generated useNotificationsHub (from Showcase.yaml's x-signalr-hubs); this adds which subscription is open.
 */
export function useNotificationHub(callbacks: NotificationsHubCallbacks) {
  const { connectionState, isConnected, connect, disconnect, methods } = useNotificationsHub(callbacks);
  const [isSubscribed, setIsSubscribed] = useState(false);
  // The id the hub gave the subscription - Unsubscribe names it
  const subscriptionIdRef = useRef<string | null>(null);

  // A subscription lives on its connection: one that closed took it along
  useEffect(() => {
    if (connectionState === 'Disconnected') {
      subscriptionIdRef.current = null;
      setIsSubscribed(false);
    }
  }, [connectionState]);

  const subscribe = useCallback(async (topics: string[]) => {
    if (!isConnected) return;

    subscriptionIdRef.current = await methods.subscribe(topics);
    setIsSubscribed(true);
  }, [isConnected, methods]);

  const unsubscribe = useCallback(async () => {
    const subscriptionId = subscriptionIdRef.current;
    if (!isConnected || !subscriptionId) return;

    await methods.unsubscribe(subscriptionId);
    subscriptionIdRef.current = null;
    setIsSubscribed(false);
  }, [isConnected, methods]);

  return {
    connectionState,
    isConnected,
    isSubscribed,
    connect,
    disconnect,
    subscribe,
    unsubscribe,
  };
}
