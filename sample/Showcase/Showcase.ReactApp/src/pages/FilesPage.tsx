import { useState, useRef } from 'react';
import {
  Box,
  Typography,
  Card,
  CardContent,
  CardActions,
  Grid2,
  Button,
  TextField,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Paper,
  CircularProgress,
  Alert,
  Stack,
} from '@mui/material';
import InsertDriveFileIcon from '@mui/icons-material/InsertDriveFile';
import ImageIcon from '@mui/icons-material/Image';
import DescriptionIcon from '@mui/icons-material/Description';
import DownloadIcon from '@mui/icons-material/Download';
import PreviewIcon from '@mui/icons-material/Preview';
import { useFileDownload } from '../hooks/useFiles';

interface SampleFile {
  id: string;
  name: string;
  type: 'image' | 'text' | 'binary';
}

const sampleFiles: SampleFile[] = [
  { id: '1', name: 'sample-readme.txt', type: 'text' },
  { id: '2', name: 'config-example.json', type: 'text' },
  { id: '3', name: 'data-sample.xml', type: 'text' },
  { id: '4', name: 'sample-image.png', type: 'image' },
  { id: '5', name: 'checkerboard.png', type: 'image' },
];

const fileIcon = (type: string) => {
  switch (type) {
    case 'image':
      return <ImageIcon sx={{ fontSize: 48 }} color="primary" />;
    case 'text':
      return <DescriptionIcon sx={{ fontSize: 48 }} color="success" />;
    default:
      return <InsertDriveFileIcon sx={{ fontSize: 48 }} color="action" />;
  }
};

export default function FilesPage() {
  const fileDownload = useFileDownload();
  const downloadLinkRef = useRef<HTMLAnchorElement>(null);

  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewContent, setPreviewContent] = useState<string | null>(null);
  const [previewType, setPreviewType] = useState<'image' | 'text' | 'binary'>('text');
  const [previewFileName, setPreviewFileName] = useState('');
  const [errorMsg, setErrorMsg] = useState('');

  const [customFileId, setCustomFileId] = useState('');

  const handlePreview = async (file: SampleFile) => {
    setErrorMsg('');
    try {
      const blob = await fileDownload.mutateAsync(file.id);
      setPreviewFileName(file.name);
      setPreviewType(file.type);

      if (file.type === 'image') {
        const url = URL.createObjectURL(blob);
        setPreviewContent(url);
      } else if (file.type === 'text') {
        const text = await blob.text();
        setPreviewContent(text);
      } else {
        setPreviewContent(`Binary file: ${blob.size} bytes`);
      }
      setPreviewOpen(true);
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to download file for preview.');
    }
  };

  const handleDownload = async (file: SampleFile) => {
    setErrorMsg('');
    try {
      const blob = await fileDownload.mutateAsync(file.id);
      const url = URL.createObjectURL(blob);
      const link = downloadLinkRef.current;
      if (link) {
        link.href = url;
        link.download = file.name;
        link.click();
        URL.revokeObjectURL(url);
      }
    } catch (err) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to download file.');
    }
  };

  const handleCustomLookup = async () => {
    if (!customFileId.trim()) return;
    const file: SampleFile = { id: customFileId.trim(), name: customFileId.trim(), type: 'binary' };
    await handlePreview(file);
  };

  const handleClosePreview = () => {
    if (previewType === 'image' && previewContent) {
      URL.revokeObjectURL(previewContent);
    }
    setPreviewOpen(false);
    setPreviewContent(null);
  };

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Files
      </Typography>

      {errorMsg && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setErrorMsg('')}>
          {errorMsg}
        </Alert>
      )}

      <Grid2 container spacing={3} sx={{ mb: 4 }}>
        {sampleFiles.map((file) => (
          <Grid2 key={file.id} size={{ xs: 12, sm: 6, md: 4 }}>
            <Card>
              <CardContent sx={{ textAlign: 'center' }}>
                {fileIcon(file.type)}
                <Typography variant="h6" sx={{ mt: 1 }}>
                  {file.name}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  ID: {file.id}
                </Typography>
              </CardContent>
              <CardActions sx={{ justifyContent: 'center' }}>
                <Button
                  size="small"
                  startIcon={<PreviewIcon />}
                  onClick={() => handlePreview(file)}
                  disabled={fileDownload.isPending}
                >
                  Preview
                </Button>
                <Button
                  size="small"
                  startIcon={<DownloadIcon />}
                  onClick={() => handleDownload(file)}
                  disabled={fileDownload.isPending}
                >
                  Download
                </Button>
              </CardActions>
            </Card>
          </Grid2>
        ))}
      </Grid2>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6" gutterBottom>
          Custom File Lookup
        </Typography>
        <Stack direction="row" spacing={2} alignItems="center">
          <TextField
            label="File ID"
            size="small"
            value={customFileId}
            onChange={(e) => setCustomFileId(e.target.value)}
          />
          <Button
            variant="contained"
            onClick={handleCustomLookup}
            disabled={fileDownload.isPending || !customFileId.trim()}
          >
            {fileDownload.isPending ? <CircularProgress size={20} /> : 'Lookup'}
          </Button>
        </Stack>
      </Paper>

      {/* Hidden download link */}
      <a ref={downloadLinkRef} style={{ display: 'none' }} />

      {/* Preview Dialog */}
      <Dialog open={previewOpen} onClose={handleClosePreview} maxWidth="md" fullWidth>
        <DialogTitle>Preview: {previewFileName}</DialogTitle>
        <DialogContent>
          {previewType === 'image' && previewContent && (
            <Box sx={{ textAlign: 'center' }}>
              <img
                src={previewContent}
                alt={previewFileName}
                style={{ maxWidth: '100%', maxHeight: '60vh' }}
              />
            </Box>
          )}
          {previewType === 'text' && previewContent && (
            <Box
              component="pre"
              sx={{
                whiteSpace: 'pre-wrap',
                wordBreak: 'break-word',
                maxHeight: '60vh',
                overflow: 'auto',
                bgcolor: 'action.hover',
                color: 'text.primary',
                p: 2,
                borderRadius: 1,
                fontFamily: 'monospace',
                fontSize: '0.875rem',
                m: 0,
              }}
            >
              {previewContent}
            </Box>
          )}
          {previewType === 'binary' && previewContent && (
            <Typography color="text.secondary">{previewContent}</Typography>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClosePreview}>Close</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
