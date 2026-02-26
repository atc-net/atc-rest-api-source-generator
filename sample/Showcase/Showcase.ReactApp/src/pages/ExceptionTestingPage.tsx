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
import { useTesting } from '../hooks/useTesting';

interface TestDefinition {
  code: number;
  description: string;
  expectedStatus: number;
}

const testDefinitions: TestDefinition[] = [
  { code: 0, description: 'No Exception', expectedStatus: 200 },
  { code: 1, description: 'Validation Error', expectedStatus: 400 },
  { code: 2, description: 'Unauthorized', expectedStatus: 401 },
  { code: 3, description: 'Forbidden', expectedStatus: 403 },
  { code: 4, description: 'Not Found', expectedStatus: 404 },
  { code: 5, description: 'Conflict', expectedStatus: 409 },
  { code: 6, description: 'Internal Server Error', expectedStatus: 500 },
  { code: 7, description: 'Not Implemented', expectedStatus: 500 },
  { code: 8, description: 'Application Exception', expectedStatus: 500 },
  { code: 9, description: 'Bad Gateway', expectedStatus: 502 },
  { code: 10, description: 'Service Unavailable', expectedStatus: 503 },
];

export default function ExceptionTestingPage() {
  const { results, isRunning, runTest, runAll, clear } = useTesting();

  const getResultForCode = (code: number) => results.find((r) => r.code === code);

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Exception Testing
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Test the API's error handling by triggering different exception types. Each test sends a
        request with a specific code and verifies the expected HTTP status response.
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
          disabled={isRunning || results.length === 0}
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
              <TableCell>Description</TableCell>
              <TableCell>Expected</TableCell>
              <TableCell>Actual</TableCell>
              <TableCell>Match</TableCell>
              <TableCell>Message</TableCell>
              <TableCell>Timestamp</TableCell>
              <TableCell align="right">Action</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {testDefinitions.map((test) => {
              const result = getResultForCode(test.code);
              return (
                <TableRow key={test.code}>
                  <TableCell>{test.code}</TableCell>
                  <TableCell>{test.description}</TableCell>
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
    </Box>
  );
}
