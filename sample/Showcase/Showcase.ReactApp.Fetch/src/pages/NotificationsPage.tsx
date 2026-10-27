import { useState, useCallback } from 'react';
import {
  Box,
  Typography,
  Paper,
  Button,
  Chip,
  Stack,
  Card,
  CardContent,
  Grid,
  Divider,
  List,
  ListItem,
  ListItemText,
} from '@mui/material';
import WifiIcon from '@mui/icons-material/Wifi';
import WifiOffIcon from '@mui/icons-material/WifiOff';
import ClearAllIcon from '@mui/icons-material/ClearAll';
import { useNotificationHub } from '../hooks/useNotificationHub';
import NotificationsStream from '../components/NotificationsStream';
import type { SystemNotification } from '../api/models/SystemNotification';

const topics = ['System', 'User', 'Data', 'Alert', 'Metric'] as const;

const severityColor = (
  severity?: string,
): 'error' | 'warning' | 'info' | 'default' => {
  switch (severity) {
    case 'Critical':
    case 'Error':
      return 'error';
    case 'Warning':
      return 'warning';
    case 'Info':
      return 'info';
    default:
      return 'default';
  }
};

export default function NotificationsPage() {
  const [notifications, setNotifications] = useState<SystemNotification[]>([]);

  const handleNotification = useCallback((n: SystemNotification) => {
    setNotifications((prev) => [n, ...prev]);
  }, []);

  const {
    connectionState,
    isConnected,
    isSubscribed,
    connect,
    disconnect,
    subscribe,
    unsubscribe,
  } = useNotificationHub({
    onSystemNotification: handleNotification,
  });

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

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Live Notifications
      </Typography>

      <Paper sx={{ p: 2, mb: 3 }}>
        <Stack sx={{ alignItems: 'center', flexWrap: 'wrap' }} direction="row" spacing={2}>
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

      <Paper sx={{ p: 2, mb: 3 }}>
        <Typography variant="h6" gutterBottom>
          Topics
        </Typography>
        <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', mb: 2 }}>
          {topics.map((topic) => (
            <Chip
              key={topic}
              label={topic}
              color={isSubscribed ? 'primary' : 'default'}
              variant={isSubscribed ? 'filled' : 'outlined'}
            />
          ))}
        </Stack>
        <Stack direction="row" spacing={1}>
          <Button
            size="small"
            variant="outlined"
            onClick={() => subscribe([...topics])}
            disabled={!isConnected || isSubscribed}
          >
            Subscribe All
          </Button>
          <Button
            size="small"
            variant="outlined"
            onClick={() => unsubscribe()}
            disabled={!isConnected || !isSubscribed}
          >
            Unsubscribe
          </Button>
        </Stack>
      </Paper>

      <NotificationsStream />

      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h6">
          Feed ({notifications.length} notifications)
        </Typography>
        <Button
          startIcon={<ClearAllIcon />}
          onClick={() => setNotifications([])}
          disabled={notifications.length === 0}
        >
          Clear
        </Button>
      </Stack>

      {notifications.length === 0 && (
        <Paper sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">
            No notifications received yet. Connect and subscribe to start receiving events.
          </Typography>
        </Paper>
      )}

      <Stack spacing={2}>
        {notifications.map((n, idx) => (
          <Card key={`${n.id}-${idx}`} variant="outlined" data-testid="notification-item">
            <CardContent>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1 }}>
                <Chip label={n.type} size="small" color="primary" variant="outlined" />
                {n.severity && (
                  <Chip label={n.severity} size="small" color={severityColor(n.severity)} />
                )}
                <Typography variant="caption" color="text.secondary" sx={{ ml: 'auto' }}>
                  {new Date(n.timestamp).toLocaleTimeString()}
                </Typography>
              </Stack>
              <Typography variant="body1">{n.message}</Typography>

              {n.metrics && (
                <>
                  <Divider sx={{ my: 1 }} />
                  <Grid container spacing={2}>
                    <Grid size={{ xs: 6, sm: 3 }}>
                      <List dense disablePadding>
                        <ListItem disableGutters>
                          <ListItemText
                            primary={`${n.metrics.cpuUsage?.toFixed(1) ?? '—'}%`}
                            secondary="CPU"
                          />
                        </ListItem>
                      </List>
                    </Grid>
                    <Grid size={{ xs: 6, sm: 3 }}>
                      <List dense disablePadding>
                        <ListItem disableGutters>
                          <ListItemText
                            primary={`${n.metrics.memoryUsage?.toFixed(0) ?? '—'}%`}
                            secondary="Memory"
                          />
                        </ListItem>
                      </List>
                    </Grid>
                    <Grid size={{ xs: 6, sm: 3 }}>
                      <List dense disablePadding>
                        <ListItem disableGutters>
                          <ListItemText
                            primary={String(n.metrics.activeConnections ?? '—')}
                            secondary="Connections"
                          />
                        </ListItem>
                      </List>
                    </Grid>
                    <Grid size={{ xs: 6, sm: 3 }}>
                      <List dense disablePadding>
                        <ListItem disableGutters>
                          <ListItemText
                            primary={`${n.metrics.requestsPerSecond?.toFixed(0) ?? '—'}/s`}
                            secondary="Requests"
                          />
                        </ListItem>
                      </List>
                    </Grid>
                  </Grid>
                  {n.metrics.uptime && (
                    <Typography variant="caption" color="text.secondary">
                      Uptime: {n.metrics.uptime}
                    </Typography>
                  )}
                </>
              )}
            </CardContent>
          </Card>
        ))}
      </Stack>
    </Box>
  );
}
