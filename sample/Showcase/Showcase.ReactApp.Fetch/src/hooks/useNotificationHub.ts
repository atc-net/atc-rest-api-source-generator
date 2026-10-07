import { useState, useRef, useCallback, useEffect } from 'react';
import { HubConnectionBuilder, HubConnection, HubConnectionState } from '@microsoft/signalr';
import { apiBaseUrl } from '../config/api';
import type { SystemNotification } from '../api/models/SystemNotification';
import type { UserActivityEvent } from '../api/models/UserActivityEvent';
import type { DataChangeEvent } from '../api/models/DataChangeEvent';

export type ConnectionState = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting';

interface NotificationHubCallbacks {
  onSystemNotification?: (notification: SystemNotification) => void;
  onUserActivity?: (event: UserActivityEvent) => void;
  onDataChange?: (event: DataChangeEvent) => void;
}

export function useNotificationHub(callbacks: NotificationHubCallbacks) {
  const [connectionState, setConnectionState] = useState<ConnectionState>('Disconnected');
  const [isSubscribed, setIsSubscribed] = useState(false);
  const connectionRef = useRef<HubConnection | null>(null);
  // The start of the current connection: a stop waits for it (stopping during the negotiation fails the start)
  const startRef = useRef<Promise<void> | null>(null);
  // The id the hub gave the subscription - Unsubscribe names it
  const subscriptionIdRef = useRef<string | null>(null);
  const callbacksRef = useRef(callbacks);
  callbacksRef.current = callbacks;

  const isConnected = connectionState === 'Connected';

  // Stops a connection once its start has settled (React's StrictMode unmounts a component right after mounting it)
  const stopWhenStarted = useCallback(async (connection: HubConnection, start: Promise<void> | null) => {
    await start?.catch(() => undefined);
    await connection.stop();
  }, []);

  const connect = useCallback(async () => {
    if (connectionRef.current) return;

    setConnectionState('Connecting');

    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/notifications`)
      .withAutomaticReconnect()
      .build();

    // Only the current connection updates the state: one that was replaced (or stopped by an unmount) is history
    const isCurrent = () => connectionRef.current === connection;
    connection.onreconnecting(() => isCurrent() && setConnectionState('Reconnecting'));
    connection.onreconnected(() => isCurrent() && setConnectionState('Connected'));
    connection.onclose(() => {
      if (!isCurrent()) return;
      connectionRef.current = null;
      subscriptionIdRef.current = null;
      setConnectionState('Disconnected');
      setIsSubscribed(false);
    });

    connection.on('SystemNotification', (notification: SystemNotification) => {
      callbacksRef.current.onSystemNotification?.(notification);
    });

    connection.on('UserActivity', (event: UserActivityEvent) => {
      callbacksRef.current.onUserActivity?.(event);
    });

    connection.on('DataChange', (event: DataChangeEvent) => {
      callbacksRef.current.onDataChange?.(event);
    });

    connectionRef.current = connection;
    const start = connection.start();
    startRef.current = start;

    try {
      await start;
      if (isCurrent()) {
        setConnectionState('Connected');
      }
    } catch {
      if (isCurrent()) {
        setConnectionState('Disconnected');
        connectionRef.current = null;
      }
    }
  }, []);

  const disconnect = useCallback(async () => {
    const connection = connectionRef.current;
    if (!connection) return;

    connectionRef.current = null;
    subscriptionIdRef.current = null;
    setIsSubscribed(false);
    try {
      await stopWhenStarted(connection, startRef.current);
    } finally {
      setConnectionState('Disconnected');
    }
  }, [stopWhenStarted]);

  const subscribe = useCallback(async (topics: string[]) => {
    const connection = connectionRef.current;
    if (!connection || connection.state !== HubConnectionState.Connected) return;

    subscriptionIdRef.current = await connection.invoke<string>('Subscribe', topics);
    setIsSubscribed(true);
  }, []);

  const unsubscribe = useCallback(async () => {
    const connection = connectionRef.current;
    const subscriptionId = subscriptionIdRef.current;
    if (!connection || connection.state !== HubConnectionState.Connected || !subscriptionId) return;

    await connection.invoke('Unsubscribe', subscriptionId);
    subscriptionIdRef.current = null;
    setIsSubscribed(false);
  }, []);

  useEffect(() => {
    return () => {
      const connection = connectionRef.current;
      connectionRef.current = null;
      subscriptionIdRef.current = null;
      if (connection) {
        void stopWhenStarted(connection, startRef.current);
      }
    };
  }, [stopWhenStarted]);

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
