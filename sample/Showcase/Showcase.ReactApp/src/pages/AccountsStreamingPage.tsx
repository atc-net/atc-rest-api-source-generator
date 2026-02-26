import {
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Button,
  LinearProgress,
  Stack,
  Chip,
} from '@mui/material';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import StopIcon from '@mui/icons-material/Stop';
import ClearAllIcon from '@mui/icons-material/ClearAll';
import { useAccountsStreaming } from '../hooks/useAccountsStreaming';

export default function AccountsStreamingPage() {
  const { accounts, isStreaming, startStreaming, cancel, clear } = useAccountsStreaming();

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Accounts (Streaming)
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Demonstrates async enumerable streaming. Items appear in the table as they arrive from the
        server.
      </Typography>

      <Stack direction="row" spacing={2} sx={{ mb: 2 }} alignItems="center">
        <Button
          variant="contained"
          startIcon={<PlayArrowIcon />}
          onClick={startStreaming}
          disabled={isStreaming}
        >
          Start Streaming
        </Button>
        <Button
          variant="outlined"
          color="warning"
          startIcon={<StopIcon />}
          onClick={cancel}
          disabled={!isStreaming}
        >
          Cancel
        </Button>
        <Button
          variant="outlined"
          startIcon={<ClearAllIcon />}
          onClick={clear}
          disabled={isStreaming}
        >
          Clear
        </Button>
        <Chip
          label={`${accounts.length} items received`}
          color={isStreaming ? 'primary' : 'default'}
          variant="outlined"
        />
      </Stack>

      {isStreaming && <LinearProgress sx={{ mb: 2 }} />}

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>ID</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Tag</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {accounts.map((account, idx) => (
              <TableRow key={`${account.id}-${idx}`}>
                <TableCell>{account.id}</TableCell>
                <TableCell>{account.name}</TableCell>
                <TableCell>{account.tag ?? ''}</TableCell>
              </TableRow>
            ))}
            {accounts.length === 0 && !isStreaming && (
              <TableRow>
                <TableCell colSpan={3} align="center">
                  No items. Click "Start Streaming" to begin.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
}
