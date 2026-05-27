import { useState, useCallback, useEffect } from 'react';
import {
  Box,
  Typography,
  Paper,
  Button,
  Chip,
  Stack,
  Card,
  CardContent,
  Divider,
} from '@mui/material';
import WifiIcon from '@mui/icons-material/Wifi';
import WifiOffIcon from '@mui/icons-material/WifiOff';
import ClearAllIcon from '@mui/icons-material/ClearAll';
import { useNotificationHub } from '../hooks/useNotificationHub';
import type { SystemNotification } from '../api/models/SystemNotification';
import type { UserActivityEvent } from '../api/models/UserActivityEvent';
import type { DataChangeEvent } from '../api/models/DataChangeEvent';

type WebhookEvent =
  | { kind: 'system'; data: SystemNotification; receivedAt: string }
  | { kind: 'user'; data: UserActivityEvent; receivedAt: string }
  | { kind: 'data'; data: DataChangeEvent; receivedAt: string };

export default function WebhookDemoPage() {
  const [events, setEvents] = useState<WebhookEvent[]>([]);

  const handleSystem = useCallback((n: SystemNotification) => {
    setEvents((prev) => [{ kind: 'system', data: n, receivedAt: new Date().toISOString() }, ...prev]);
  }, []);

  const handleUser = useCallback((n: UserActivityEvent) => {
    setEvents((prev) => [{ kind: 'user', data: n, receivedAt: new Date().toISOString() }, ...prev]);
  }, []);

  const handleData = useCallback((n: DataChangeEvent) => {
    setEvents((prev) => [{ kind: 'data', data: n, receivedAt: new Date().toISOString() }, ...prev]);
  }, []);

  const {
    connectionState,
    isConnected,
    connect,
    disconnect,
    subscribe,
  } = useNotificationHub({
    onSystemNotification: handleSystem,
    onUserActivity: handleUser,
    onDataChange: handleData,
  });

  // Auto-connect and subscribe to all topics on mount
  useEffect(() => {
    if (!isConnected) {
      connect();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Subscribe to all topics once connected
  useEffect(() => {
    if (isConnected) {
      subscribe(['System', 'User', 'Data', 'Alert', 'Metric']);
    }
  }, [isConnected, subscribe]);

  const connectionColor = (): 'success' | 'warning' | 'default' => {
    switch (connectionState) {
      case 'Connected':
        return 'success';
      case 'Reconnecting':
        return 'warning';
      default:
        return 'default';
    }
  };

  const eventChipColor = (kind: string): 'primary' | 'secondary' | 'warning' => {
    switch (kind) {
      case 'system':
        return 'primary';
      case 'user':
        return 'secondary';
      case 'data':
        return 'warning';
      default:
        return 'primary';
    }
  };

  const renderEventContent = (event: WebhookEvent) => {
    switch (event.kind) {
      case 'system':
        return (
          <>
            <Typography variant="body1">{event.data.message}</Typography>
            {event.data.severity && (
              <Chip label={event.data.severity} size="small" sx={{ mt: 0.5 }} />
            )}
          </>
        );
      case 'user':
        return (
          <>
            <Typography variant="body1">
              User {event.data.userId} performed {event.data.action}
            </Typography>
            {event.data.details && (
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                {event.data.details}
              </Typography>
            )}
          </>
        );
      case 'data':
        return (
          <>
            <Typography variant="body1">
              {event.data.operation} on {event.data.entityType} ({event.data.entityId})
            </Typography>
            {event.data.performedBy && (
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                By: {event.data.performedBy}
              </Typography>
            )}
          </>
        );
    }
  };

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Webhook Demo
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
        Connects to the SignalR hub and subscribes to all event topics. Events from the server
        appear in real-time below.
      </Typography>

      <Paper sx={{ p: 2, mb: 3 }}>
        <Stack direction="row" spacing={2} alignItems="center">
          <Button
            variant="contained"
            startIcon={<WifiIcon />}
            onClick={connect}
            disabled={isConnected}
          >
            Connect
          </Button>
          <Button
            variant="outlined"
            startIcon={<WifiOffIcon />}
            onClick={disconnect}
            disabled={!isConnected}
          >
            Disconnect
          </Button>
          <Chip
            label={connectionState ?? 'Disconnected'}
            color={connectionColor()}
            variant="outlined"
          />
        </Stack>
      </Paper>

      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
        <Typography variant="h6">
          Events ({events.length})
        </Typography>
        <Button
          startIcon={<ClearAllIcon />}
          onClick={() => setEvents([])}
          disabled={events.length === 0}
        >
          Clear
        </Button>
      </Stack>

      {events.length === 0 && (
        <Paper sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">
            Waiting for events... The server sends notifications automatically when connected.
          </Typography>
        </Paper>
      )}

      <Stack spacing={2}>
        {events.map((event, idx) => (
          <Card key={`${event.kind}-${idx}`} variant="outlined">
            <CardContent>
              <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
                <Chip
                  label={event.kind.toUpperCase()}
                  size="small"
                  color={eventChipColor(event.kind)}
                />
                <Typography variant="caption" color="text.secondary" sx={{ ml: 'auto' }}>
                  {new Date(event.receivedAt).toLocaleTimeString()}
                </Typography>
              </Stack>
              <Divider sx={{ mb: 1 }} />
              {renderEventContent(event)}
            </CardContent>
          </Card>
        ))}
      </Stack>
    </Box>
  );
}
