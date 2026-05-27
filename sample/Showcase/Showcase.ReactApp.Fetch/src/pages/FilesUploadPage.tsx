import { useState, useRef } from 'react';
import {
  Box,
  Typography,
  Paper,
  Tabs,
  Tab,
  Button,
  TextField,
  CircularProgress,
  Alert,
  Stack,
  List,
  ListItem,
  ListItemText,
} from '@mui/material';
import CloudUploadIcon from '@mui/icons-material/CloudUpload';
import {
  useUploadSingleFile,
  useUploadMultiFiles,
  useUploadWithMetadata,
  useUploadMultiWithMetadata,
} from '../hooks/useFiles';

interface TabPanelProps {
  children: React.ReactNode;
  value: number;
  index: number;
}

function TabPanel({ children, value, index }: TabPanelProps) {
  if (value !== index) return null;
  return <Box sx={{ py: 3 }}>{children}</Box>;
}

export default function FilesUploadPage() {
  const [activeTab, setActiveTab] = useState(0);

  // Single file
  const singleFileRef = useRef<HTMLInputElement>(null);
  const [singleFile, setSingleFile] = useState<File | null>(null);
  const uploadSingle = useUploadSingleFile();
  const [singleMsg, setSingleMsg] = useState<{ type: 'success' | 'error'; text: string } | null>(
    null,
  );

  // Multi files
  const multiFileRef = useRef<HTMLInputElement>(null);
  const [multiFiles, setMultiFiles] = useState<File[]>([]);
  const uploadMulti = useUploadMultiFiles();
  const [multiMsg, setMultiMsg] = useState<{ type: 'success' | 'error'; text: string } | null>(
    null,
  );

  // Single with metadata
  const metaFileRef = useRef<HTMLInputElement>(null);
  const [metaFile, setMetaFile] = useState<File | null>(null);
  const [itemName, setItemName] = useState('');
  const [items, setItems] = useState('');
  const uploadWithMeta = useUploadWithMetadata();
  const [metaMsg, setMetaMsg] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  // Multi with metadata
  const multiMetaFileRef = useRef<HTMLInputElement>(null);
  const [multiMetaFiles, setMultiMetaFiles] = useState<File[]>([]);
  const uploadMultiMeta = useUploadMultiWithMetadata();
  const [multiMetaMsg, setMultiMetaMsg] = useState<{
    type: 'success' | 'error';
    text: string;
  } | null>(null);

  const handleSingleUpload = async () => {
    if (!singleFile) return;
    setSingleMsg(null);
    try {
      await uploadSingle.mutateAsync(singleFile);
      setSingleMsg({ type: 'success', text: 'File uploaded successfully.' });
      setSingleFile(null);
      if (singleFileRef.current) singleFileRef.current.value = '';
    } catch (err) {
      setSingleMsg({
        type: 'error',
        text: err instanceof Error ? err.message : 'Upload failed.',
      });
    }
  };

  const handleMultiUpload = async () => {
    if (multiFiles.length === 0) return;
    setMultiMsg(null);
    try {
      await uploadMulti.mutateAsync(multiFiles);
      setMultiMsg({ type: 'success', text: `${multiFiles.length} file(s) uploaded successfully.` });
      setMultiFiles([]);
      if (multiFileRef.current) multiFileRef.current.value = '';
    } catch (err) {
      setMultiMsg({
        type: 'error',
        text: err instanceof Error ? err.message : 'Upload failed.',
      });
    }
  };

  const handleMetaUpload = async () => {
    if (!metaFile) return;
    setMetaMsg(null);
    try {
      const itemList = items
        .split(',')
        .map((s) => s.trim())
        .filter(Boolean);
      await uploadWithMeta.mutateAsync({ file: metaFile, itemName, items: itemList });
      setMetaMsg({ type: 'success', text: 'File with metadata uploaded successfully.' });
      setMetaFile(null);
      setItemName('');
      setItems('');
      if (metaFileRef.current) metaFileRef.current.value = '';
    } catch (err) {
      setMetaMsg({
        type: 'error',
        text: err instanceof Error ? err.message : 'Upload failed.',
      });
    }
  };

  const handleMultiMetaUpload = async () => {
    if (multiMetaFiles.length === 0) return;
    setMultiMetaMsg(null);
    try {
      await uploadMultiMeta.mutateAsync({ files: multiMetaFiles });
      setMultiMetaMsg({
        type: 'success',
        text: `${multiMetaFiles.length} file(s) with metadata uploaded successfully.`,
      });
      setMultiMetaFiles([]);
      if (multiMetaFileRef.current) multiMetaFileRef.current.value = '';
    } catch (err) {
      setMultiMetaMsg({
        type: 'error',
        text: err instanceof Error ? err.message : 'Upload failed.',
      });
    }
  };

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        File Upload
      </Typography>

      <Paper sx={{ px: 3 }}>
        <Tabs value={activeTab} onChange={(_, v) => setActiveTab(v)}>
          <Tab label="Single File" />
          <Tab label="Multiple Files" />
          <Tab label="File with Metadata" />
          <Tab label="Multi with Metadata" />
        </Tabs>

        {/* Tab 0: Single File */}
        <TabPanel value={activeTab} index={0}>
          {singleMsg && (
            <Alert severity={singleMsg.type} sx={{ mb: 2 }} onClose={() => setSingleMsg(null)}>
              {singleMsg.text}
            </Alert>
          )}
          <Stack spacing={2}>
            <input
              type="file"
              ref={singleFileRef}
              onChange={(e) => setSingleFile(e.target.files?.[0] ?? null)}
            />
            {singleFile && (
              <Typography variant="body2" color="text.secondary">
                Selected: {singleFile.name} ({(singleFile.size / 1024).toFixed(1)} KB)
              </Typography>
            )}
            <Box>
              <Button
                variant="contained"
                startIcon={
                  uploadSingle.isPending ? (
                    <CircularProgress size={18} />
                  ) : (
                    <CloudUploadIcon />
                  )
                }
                onClick={handleSingleUpload}
                disabled={!singleFile || uploadSingle.isPending}
              >
                Upload
              </Button>
            </Box>
          </Stack>
        </TabPanel>

        {/* Tab 1: Multiple Files */}
        <TabPanel value={activeTab} index={1}>
          {multiMsg && (
            <Alert severity={multiMsg.type} sx={{ mb: 2 }} onClose={() => setMultiMsg(null)}>
              {multiMsg.text}
            </Alert>
          )}
          <Stack spacing={2}>
            <input
              type="file"
              multiple
              ref={multiFileRef}
              onChange={(e) => setMultiFiles(Array.from(e.target.files ?? []))}
            />
            {multiFiles.length > 0 && (
              <List dense>
                {multiFiles.map((f, i) => (
                  <ListItem key={i}>
                    <ListItemText
                      primary={f.name}
                      secondary={`${(f.size / 1024).toFixed(1)} KB`}
                    />
                  </ListItem>
                ))}
              </List>
            )}
            <Box>
              <Button
                variant="contained"
                startIcon={
                  uploadMulti.isPending ? (
                    <CircularProgress size={18} />
                  ) : (
                    <CloudUploadIcon />
                  )
                }
                onClick={handleMultiUpload}
                disabled={multiFiles.length === 0 || uploadMulti.isPending}
              >
                Upload {multiFiles.length > 0 ? `(${multiFiles.length})` : ''}
              </Button>
            </Box>
          </Stack>
        </TabPanel>

        {/* Tab 2: File with Metadata */}
        <TabPanel value={activeTab} index={2}>
          {metaMsg && (
            <Alert severity={metaMsg.type} sx={{ mb: 2 }} onClose={() => setMetaMsg(null)}>
              {metaMsg.text}
            </Alert>
          )}
          <Stack spacing={2}>
            <input
              type="file"
              ref={metaFileRef}
              onChange={(e) => setMetaFile(e.target.files?.[0] ?? null)}
            />
            {metaFile && (
              <Typography variant="body2" color="text.secondary">
                Selected: {metaFile.name}
              </Typography>
            )}
            <TextField
              label="Item Name"
              size="small"
              value={itemName}
              onChange={(e) => setItemName(e.target.value)}
              sx={{ maxWidth: 400 }}
            />
            <TextField
              label="Items (comma-separated)"
              size="small"
              value={items}
              onChange={(e) => setItems(e.target.value)}
              sx={{ maxWidth: 400 }}
              helperText="Enter item values separated by commas"
            />
            <Box>
              <Button
                variant="contained"
                startIcon={
                  uploadWithMeta.isPending ? (
                    <CircularProgress size={18} />
                  ) : (
                    <CloudUploadIcon />
                  )
                }
                onClick={handleMetaUpload}
                disabled={!metaFile || uploadWithMeta.isPending}
              >
                Upload with Metadata
              </Button>
            </Box>
          </Stack>
        </TabPanel>

        {/* Tab 3: Multi with Metadata */}
        <TabPanel value={activeTab} index={3}>
          {multiMetaMsg && (
            <Alert
              severity={multiMetaMsg.type}
              sx={{ mb: 2 }}
              onClose={() => setMultiMetaMsg(null)}
            >
              {multiMetaMsg.text}
            </Alert>
          )}
          <Stack spacing={2}>
            <input
              type="file"
              multiple
              ref={multiMetaFileRef}
              onChange={(e) => setMultiMetaFiles(Array.from(e.target.files ?? []))}
            />
            {multiMetaFiles.length > 0 && (
              <List dense>
                {multiMetaFiles.map((f, i) => (
                  <ListItem key={i}>
                    <ListItemText
                      primary={f.name}
                      secondary={`${(f.size / 1024).toFixed(1)} KB`}
                    />
                  </ListItem>
                ))}
              </List>
            )}
            <Box>
              <Button
                variant="contained"
                startIcon={
                  uploadMultiMeta.isPending ? (
                    <CircularProgress size={18} />
                  ) : (
                    <CloudUploadIcon />
                  )
                }
                onClick={handleMultiMetaUpload}
                disabled={multiMetaFiles.length === 0 || uploadMultiMeta.isPending}
              >
                Upload {multiMetaFiles.length > 0 ? `(${multiMetaFiles.length})` : ''}
              </Button>
            </Box>
          </Stack>
        </TabPanel>
      </Paper>
    </Box>
  );
}
