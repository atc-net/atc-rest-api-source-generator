import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Grid2,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import StopIcon from '@mui/icons-material/Stop';
import { ApiError } from '../api/errors';
import {
  useListPriceElementsStream,
  useListPriceEventsStream,
  useListPriceLinesStream,
  useListPricePartsStream,
  useListPriceRecordsStream,
} from '../api/hooks/useStreams';
import type { PriceTick } from '../api/models';

type Format = 'sse' | 'lines' | 'array' | 'seq' | 'mixed';

// The five formats the API streams prices in. Each is an operation of its own: the format of a response is its
// media type in the specification, and the generated client has a hook per operation.
const formats: readonly { key: Format; label: string; mediaType: string; hook: string }[] = [
  { key: 'sse', label: 'Server-Sent Events', mediaType: 'text/event-stream', hook: 'useListPriceEventsStream' },
  { key: 'lines', label: 'JSON Lines', mediaType: 'application/jsonl', hook: 'useListPriceLinesStream' },
  { key: 'array', label: 'JSON array', mediaType: 'application/json', hook: 'useListPriceElementsStream' },
  { key: 'seq', label: 'JSON Text Sequence', mediaType: 'application/json-seq', hook: 'useListPriceRecordsStream' },
  { key: 'mixed', label: 'Multipart', mediaType: 'multipart/mixed', hook: 'useListPricePartsStream' },
];

const count = 40;

interface Failure {
  status: number;
  problem: string;
  retryAfter: string;
}

// A response header by name, from the Headers of a fetch response or the header object of an axios one
function readHeader(headers: unknown, name: string): string {
  if (headers instanceof Headers) {
    return headers.get(name) ?? '';
  }

  const value = (headers as Record<string, unknown> | undefined)?.[name.toLowerCase()];
  return value === undefined || value === null ? '' : String(value);
}

export default function LiveStreamsPage() {
  const [format, setFormat] = useState<Format>('sse');
  const [enabled, setEnabled] = useState(false);
  const [closed, setClosed] = useState(false);
  const openedAt = useRef(0);
  const arrivedAt = useRef<number[]>([]);

  // One hook per operation; only the selected one is enabled
  const query = useMemo(() => ({ count, closed }), [closed]);
  const streams = {
    sse: useListPriceEventsStream(query, { enabled: enabled && format === 'sse' }),
    lines: useListPriceLinesStream(query, { enabled: enabled && format === 'lines' }),
    array: useListPriceElementsStream(query, { enabled: enabled && format === 'array' }),
    seq: useListPriceRecordsStream(query, { enabled: enabled && format === 'seq' }),
    mixed: useListPricePartsStream(query, { enabled: enabled && format === 'mixed' }),
  };
  const { items, status, error, cancel, reset } = streams[format];
  const isStreaming = status === 'streaming';

  // When each item arrived, counted from the moment the stream opened
  while (arrivedAt.current.length < items.length) {
    arrivedAt.current.push(Math.round(performance.now() - openedAt.current));
  }

  useEffect(() => {
    if (status === 'success' || status === 'error') {
      setEnabled(false);
    }
  }, [status]);

  const start = (asClosed: boolean) => {
    reset();
    arrivedAt.current = [];
    openedAt.current = performance.now();
    setClosed(asClosed);
    setEnabled(true);
  };

  const stop = () => {
    setEnabled(false);
    cancel();
  };

  const quotes = useMemo(() => {
    const bySymbol = new Map<string, { tick: PriceTick; ticks: number }>();
    for (const tick of items) {
      bySymbol.set(tick.symbol, { tick, ticks: (bySymbol.get(tick.symbol)?.ticks ?? 0) + 1 });
    }

    return [...bySymbol.values()].sort((a, b) => a.tick.symbol.localeCompare(b.tick.symbol));
  }, [items]);

  // A stream can fail before it starts: the 503 of a closed exchange is the client's ApiError, with the problem
  // and the response (its Retry-After header) on it
  const failure: Failure | null =
    error instanceof ApiError
      ? {
          status: error.status,
          problem: `${error.title ?? ''} – ${error.detail ?? ''}`,
          retryAfter: readHeader(error.response?.headers, 'Retry-After'),
        }
      : null;

  const statusText = isStreaming
    ? 'Streaming…'
    : status === 'success'
      ? `Completed: ${items.length} prices`
      : status === 'error'
        ? `Failed${failure ? `: ${failure.status}` : ''}`
        : items.length > 0
          ? `Stopped after ${items.length} prices`
          : 'Idle';

  const selected = formats.find((f) => f.key === format)!;
  const arrivals = items
    .map((tick, index) => ({ tick, elapsed: arrivedAt.current[index] ?? 0 }))
    .slice(-12)
    .reverse();

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Live Streams
      </Typography>
      <Alert severity="info" sx={{ mb: 3 }}>
        The API streams the same prices in five formats: Server-Sent Events, JSON Lines, a JSON array, a JSON Text
        Sequence and multipart/mixed. Each is an operation in the specification; the generated client reads all five
        the same way, one item at a time as it arrives.
      </Alert>

      <Stack direction="row" spacing={2} sx={{ mb: 2 }} alignItems="center" flexWrap="wrap" useFlexGap>
        <ToggleButtonGroup
          exclusive
          size="small"
          value={format}
          disabled={isStreaming}
          onChange={(_, value: Format | null) => {
            if (value) {
              setEnabled(false);
              setFormat(value);
            }
          }}
        >
          {formats.map((f) => (
            <ToggleButton key={f.key} value={f.key} data-testid={`stream-format-${f.key}`}>
              {f.label}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>
        <Button
          variant="contained"
          startIcon={<PlayArrowIcon />}
          onClick={() => start(false)}
          disabled={isStreaming}
          data-testid="stream-start"
        >
          Start
        </Button>
        <Button
          variant="outlined"
          color="warning"
          startIcon={<StopIcon />}
          onClick={stop}
          disabled={!isStreaming}
          data-testid="stream-stop"
        >
          Stop
        </Button>
        <Typography data-testid="stream-status">{statusText}</Typography>
      </Stack>

      <Stack direction="row" spacing={2} sx={{ mb: 2 }} alignItems="center">
        <Typography variant="subtitle1">Items</Typography>
        <Chip label={items.length} color={isStreaming ? 'primary' : 'default'} variant="outlined" data-testid="stream-items" />
        <Typography variant="subtitle1">Content-Type</Typography>
        <Chip label={selected.mediaType} variant="outlined" />
      </Stack>

      <Grid2 container spacing={2} sx={{ mb: 3 }}>
        {quotes.map(({ tick, ticks }) => (
          <Grid2 key={tick.symbol} size={{ xs: 6, md: 3 }}>
            <Card data-testid="stream-quote">
              <CardContent>
                <Typography variant="subtitle2">{tick.symbol}</Typography>
                <Typography variant="h5">{tick.price.toFixed(2)}</Typography>
                <Typography color={tick.change >= 0 ? 'success.main' : 'error.main'}>
                  {tick.change >= 0 ? '▲' : '▼'} {Math.abs(tick.change).toFixed(2)}
                </Typography>
                <Typography variant="caption">{ticks} ticks</Typography>
              </CardContent>
            </Card>
          </Grid2>
        ))}
      </Grid2>

      <Grid2 container spacing={2} sx={{ mb: 3 }}>
        <Grid2 size={{ xs: 12, md: 6 }}>
          <Typography variant="h6">Arrivals</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            Time since the stream opened, as the page was handed each batch.
          </Typography>
          <TableContainer component={Paper}>
            <Table size="small" data-testid="stream-arrivals">
              <TableHead>
                <TableRow>
                  <TableCell>Arrived</TableCell>
                  <TableCell>#</TableCell>
                  <TableCell>Symbol</TableCell>
                  <TableCell>Price</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {arrivals.map(({ tick, elapsed }) => (
                  <TableRow key={tick.sequence}>
                    <TableCell>+{elapsed} ms</TableCell>
                    <TableCell>{tick.sequence}</TableCell>
                    <TableCell>{tick.symbol}</TableCell>
                    <TableCell>{tick.price.toFixed(2)}</TableCell>
                  </TableRow>
                ))}
                {arrivals.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={4} align="center">
                      Press Start to open the stream.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
        </Grid2>
        <Grid2 size={{ xs: 12, md: 6 }}>
          <Typography variant="h6">The call</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            What this page runs for the selected format - the hook and the client method under it are generated.
          </Typography>
          <Paper variant="outlined" sx={{ p: 2, overflowX: 'auto' }}>
            <pre style={{ margin: 0 }}>
              <code data-testid="stream-call">{`const { items, status, error, cancel } = ${selected.hook}(
  { count: ${count} },
  { enabled },
);

// or without React:
for await (const tick of api.streams.${selected.hook.replace(/^use/, '').replace(/Stream$/, '').replace(/^./, (c) => c.toLowerCase())}({ count: ${count} }, signal)) {
  show(tick);
}`}</code>
            </pre>
          </Paper>
        </Grid2>
      </Grid2>

      <Card>
        <CardContent>
          <Typography variant="h6">When the exchange is closed</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            A stream can fail before it starts. The API answers 503 with a problem body and a Retry-After header; the
            client throws its ApiError with both.
          </Typography>
          {failure && (
            <Table size="small" sx={{ mb: 2 }} data-testid="stream-failure">
              <TableBody>
                <TableRow>
                  <TableCell>status</TableCell>
                  <TableCell data-testid="stream-failure-status">{failure.status}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell>title – detail</TableCell>
                  <TableCell data-testid="stream-failure-problem">{failure.problem}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell>Retry-After</TableCell>
                  <TableCell data-testid="stream-failure-retry">{failure.retryAfter}</TableCell>
                </TableRow>
              </TableBody>
            </Table>
          )}
          <Button variant="outlined" color="warning" onClick={() => start(true)} disabled={isStreaming} data-testid="stream-closed">
            Call a closed exchange
          </Button>
        </CardContent>
      </Card>
    </Box>
  );
}
