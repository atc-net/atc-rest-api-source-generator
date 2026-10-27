import {
  Box,
  Typography,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Button,
  CircularProgress,
  Stack,
  Chip,
} from '@mui/material';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import ClearAllIcon from '@mui/icons-material/ClearAll';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import CancelIcon from '@mui/icons-material/Cancel';
import { useTesting, exceptionTests, errorStatuses } from '../hooks/useTesting';

export default function ExceptionTestingPage() {
  const { results, errorResults, isRunning, runTest, runAll, runErrorResponseTest, clear } =
    useTesting();

  const getResultForCode = (code: number) => results.filter((r) => r.code === code).pop();

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Exception Testing
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Test the API's error handling by triggering different exception types. Each test sends a
        request with a specific code and verifies the expected HTTP status response. Every error is
        an RFC 9457 ProblemDetails, and the message shown is its <code>detail</code>.
      </Typography>

      <Stack direction="row" spacing={2} sx={{ mb: 3 }}>
        <Button
          variant="contained"
          startIcon={isRunning ? <CircularProgress size={18} /> : <PlayArrowIcon />}
          onClick={runAll}
          disabled={isRunning}
        >
          Run All Tests
        </Button>
        <Button
          variant="outlined"
          startIcon={<ClearAllIcon />}
          onClick={clear}
          disabled={isRunning || (results.length === 0 && errorResults.length === 0)}
        >
          Clear Results
        </Button>
        {results.length > 0 && (
          <Chip
            label={`${results.filter((r) => r.match).length}/${results.length} passed`}
            color={results.every((r) => r.match) ? 'success' : 'warning'}
          />
        )}
      </Stack>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Code</TableCell>
              <TableCell>Exception</TableCell>
              <TableCell>Expected</TableCell>
              <TableCell>Actual</TableCell>
              <TableCell>Match</TableCell>
              <TableCell>Message</TableCell>
              <TableCell>Timestamp</TableCell>
              <TableCell align="right">Action</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {exceptionTests.map((test) => {
              const result = getResultForCode(test.code);
              return (
                <TableRow key={test.code}>
                  <TableCell>{test.code}</TableCell>
                  <TableCell>{test.exceptionType}</TableCell>
                  <TableCell>
                    <Chip label={test.expectedStatus} size="small" variant="outlined" />
                  </TableCell>
                  <TableCell>
                    {result ? (
                      <Chip
                        label={result.actualStatus}
                        size="small"
                        color={result.match ? 'success' : 'error'}
                      />
                    ) : (
                      '—'
                    )}
                  </TableCell>
                  <TableCell>
                    {result ? (
                      result.match ? (
                        <CheckCircleIcon color="success" />
                      ) : (
                        <CancelIcon color="error" />
                      )
                    ) : (
                      '—'
                    )}
                  </TableCell>
                  <TableCell sx={{ maxWidth: 250, overflow: 'hidden', textOverflow: 'ellipsis' }}>
                    {result?.message ?? '—'}
                  </TableCell>
                  <TableCell>
                    {result?.timestamp
                      ? new Date(result.timestamp).toLocaleTimeString()
                      : '—'}
                  </TableCell>
                  <TableCell align="right">
                    <Button
                      size="small"
                      variant="outlined"
                      onClick={() => runTest(test.code)}
                      disabled={isRunning}
                    >
                      Test
                    </Button>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>

      <Typography variant="h5" sx={{ mt: 5 }} gutterBottom>
        Error Responses
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        The handler returns the requested status through the generated result factories. The
        generated client maps it to a typed arm (<code>badRequest</code>, <code>notFound</code>, …)
        whose <code>ApiError</code> exposes the ProblemDetails <code>title</code> and{' '}
        <code>detail</code>; a 400 is a <code>ValidationError</code> with its <code>errors</code>.
      </Typography>

      <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
        {errorStatuses.map((status) => (
          <Button
            key={status}
            size="small"
            variant="outlined"
            onClick={() => runErrorResponseTest(status)}
            disabled={isRunning}
          >
            {status}
          </Button>
        ))}
      </Stack>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Status</TableCell>
              <TableCell>Arm</TableCell>
              <TableCell>Title</TableCell>
              <TableCell>Detail</TableCell>
              <TableCell>Errors</TableCell>
              <TableCell>Retry-After</TableCell>
              <TableCell>Timestamp</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {errorResults.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7}>No error responses requested yet.</TableCell>
              </TableRow>
            ) : (
              errorResults.map((result) => (
                <TableRow key={result.timestamp}>
                  <TableCell>
                    <Chip label={result.status} size="small" color="warning" />
                  </TableCell>
                  <TableCell>
                    <code>{result.arm}</code>
                  </TableCell>
                  <TableCell>{result.title ?? '—'}</TableCell>
                  <TableCell>{result.detail ?? '—'}</TableCell>
                  <TableCell>{result.errors ?? '—'}</TableCell>
                  <TableCell>{result.retryAfter ?? '—'}</TableCell>
                  <TableCell>{new Date(result.timestamp).toLocaleTimeString()}</TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
}
