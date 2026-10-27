import { useState } from 'react';
import { Button, Chip, Paper, Stack, Typography } from '@mui/material';
import { useListNotificationsStream } from '../api/hooks/useNotifications';

/**
 * The notifications as one HTTP response that never ends (`listNotifications`, an async enumerable on the server):
 * the generated stream hook reads each event as it arrives - no SignalR. The server sends one every 5-10 seconds.
 */
export default function NotificationsStream() {
  const [streaming, setStreaming] = useState(false);
  const { items, status, error } = useListNotificationsStream({ topics: 'system,user,data' }, { enabled: streaming });

  return (
    <Paper sx={{ p: 2, mb: 3 }}>
      <Typography variant="h6" gutterBottom>
        HTTP stream
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        <code>GET /notifications/stream</code> - one response, read event by event while the server writes it.
      </Typography>
      <Stack direction="row" spacing={2} sx={{ alignItems: 'center', mb: 2 }}>
        <Button variant="contained" onClick={() => setStreaming(true)} disabled={streaming}>
          Start Stream
        </Button>
        <Button variant="outlined" onClick={() => setStreaming(false)} disabled={!streaming}>
          Stop Stream
        </Button>
        <Chip label={streaming ? `Streaming - ${items.length} received` : 'Stopped'} variant="outlined" data-testid="notifications-stream-status" />
      </Stack>
      {error && (
        <Typography color="error" variant="body2">
          {status}: {error.message}
        </Typography>
      )}
      {items.map((item) => (
        <Typography key={item.id} variant="body2" data-testid="notifications-stream-item">
          {`${item.type}: ${item.message ?? ''}`}
        </Typography>
      ))}
    </Paper>
  );
}
