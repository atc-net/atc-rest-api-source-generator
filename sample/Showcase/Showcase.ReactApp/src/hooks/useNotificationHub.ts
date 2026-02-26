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
  const callbacksRef = useRef(callbacks);
  callbacksRef.current = callbacks;

  const isConnected = connectionState === 'Connected';

  const connect = useCallback(async () => {
    if (connectionRef.current) return;

    setConnectionState('Connecting');

    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/notifications`)
      .withAutomaticReconnect()
      .build();

    connection.onreconnecting(() => setConnectionState('Reconnecting'));
    connection.onreconnected(() => setConnectionState('Connected'));
    connection.onclose(() => {
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

    try {
      await connection.start();
      setConnectionState('Connected');
    } catch {
      setConnectionState('Disconnected');
      connectionRef.current = null;
    }
  }, []);

  const disconnect = useCallback(async () => {
    const connection = connectionRef.current;
    if (!connection) return;

    connectionRef.current = null;
    setIsSubscribed(false);
    try {
      await connection.stop();
    } finally {
      setConnectionState('Disconnected');
    }
  }, []);

  const subscribe = useCallback(async (topics: string[]) => {
    const connection = connectionRef.current;
    if (!connection || connection.state !== HubConnectionState.Connected) return;

    await connection.invoke<string>('Subscribe', topics);
    setIsSubscribed(true);
  }, []);

  const unsubscribe = useCallback(async () => {
    const connection = connectionRef.current;
    if (!connection || connection.state !== HubConnectionState.Connected) return;

    await connection.invoke('Unsubscribe');
    setIsSubscribed(false);
  }, []);

  useEffect(() => {
    return () => {
      connectionRef.current?.stop();
      connectionRef.current = null;
    };
  }, []);

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
