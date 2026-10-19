import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { documentsClient } from '../config/api';
import { getDownloadFileName } from '../api/helpers/download';
import type { Document, DocumentSummary } from '../api/models';

const formatSize = (bytes: number) =>
  bytes < 1024 ? `${bytes} B` : bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(1)} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;

// Hands a Blob to the browser as a download with the given file name.
function save(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const anchor = window.document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  window.document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}

/**
 * PDF, Excel, PNG and JPEG files sent as the raw request body with their own media type, downloads with their media
 * type and file name, and a summary that is a model or a file.
 */
export default function DocumentsPage() {
  const [documents, setDocuments] = useState<Document[]>([]);
  const [status, setStatus] = useState('Choose a PDF, an Excel workbook, a PNG or a JPEG.');
  const [preview, setPreview] = useState<{ url: string; fileName: string } | null>(null);
  const [previewMessage, setPreviewMessage] = useState<string | null>(null);
  const [summary, setSummary] = useState<DocumentSummary | null>(null);
  const [summaryETag, setSummaryETag] = useState<string | undefined>(undefined);
  const [summaryCheck, setSummaryCheck] = useState<string | null>(null);
  const [copyStatus, setCopyStatus] = useState<string | null>(null);

  const load = useCallback(async () => {
    const result = await documentsClient.listDocuments();
    setDocuments(result.status === 'ok' ? result.data : []);
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const upload = async (files: FileList | null) => {
    for (const file of Array.from(files ?? [])) {
      // The file is the request body, with its own media type
      const result = await documentsClient.uploadDocument(file, { fileName: file.name });
      setStatus(result.status === 'created'
        ? `Uploaded ${result.data.fileName} as ${result.data.contentType} (${formatSize(result.data.length)})`
        : `Upload of ${file.name} failed: ${result.error.message}`);
    }

    await load();
  };

  const download = async (doc: Document) => {
    const result = await documentsClient.getDocument(doc.id);
    if (result.status === 'ok') {
      // The file name comes from the response's Content-Disposition (the API exposes the header to the browser)
      save(result.data, getDownloadFileName(result.response) ?? doc.fileName);
    }
  };

  const showPreview = async (doc: Document) => {
    setPreview(null);
    setPreviewMessage(null);
    const result = await documentsClient.getDocument(doc.id);
    if (result.status !== 'ok') {
      setPreviewMessage('Document not found');
      return;
    }

    const fileName = getDownloadFileName(result.response) ?? doc.fileName;
    if (result.data.type.startsWith('image/')) {
      setPreview({ url: URL.createObjectURL(result.data), fileName });
    } else {
      setPreviewMessage(`${fileName} is ${result.data.type} (${formatSize(result.data.size)}) - download it to open it.`);
    }
  };

  const showSummary = async (doc: Document, printable: boolean) => {
    const result = await documentsClient.getDocumentSummary(doc.id, { printable });
    if (result.status !== 'ok') {
      return;
    }

    // A model or a file: the PDF is a Blob, the summary the model
    if (result.data instanceof Blob) {
      save(result.data, getDownloadFileName(result.response) ?? 'summary.pdf');
    } else {
      // The JSON summary with its ETag - the version a later request sends back as If-None-Match (a header the
      // response declares is typed on its result; the API exposes ETag to the browser)
      setSummary(result.data);
      setSummaryETag(result.headers.ETag);
      setSummaryCheck(null);
    }
  };

  const checkSummary = async () => {
    if (!summary) {
      return;
    }

    const result = await documentsClient.getDocumentSummary(summary.id, { printable: false }, { 'If-None-Match': summaryETag });
    if (result.status === 'notModified') {
      setSummaryCheck(`Not modified (304) - ${result.headers.ETag ?? summaryETag} is current`);
    } else if (result.status === 'ok' && !(result.data instanceof Blob)) {
      setSummary(result.data);
      setSummaryETag(result.headers.ETag);
      setSummaryCheck('Changed - the summary was read again');
    } else {
      setSummaryCheck('The document is gone');
    }
  };

  const copy = async (doc: Document) => {
    // 303 See Other to the copy's summary, followed by the browser: the ok arm holds the summary's JSON
    const result = await documentsClient.copyDocument(doc.id);
    const copied = result.status === 'ok' ? (result.data as unknown as DocumentSummary | undefined) : undefined;
    setCopyStatus(copied?.fileName
      ? `Copied as ${copied.fileName} (303 See Other, followed to its summary)`
      : `Copy of ${doc.fileName} failed`);
    await load();
  };

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Documents
      </Typography>
      <Typography variant="body2" sx={{ mb: 3 }}>
        PDF, Excel, PNG and JPEG files sent as the raw request body, with their own media type (the upload declares
        several, and the server's handler gets the one that arrived). A download comes back with its media type and its
        file name, and the summary is a model or a file: JSON, or the same summary as a PDF.
      </Typography>

      <Paper sx={{ p: 3, mb: 3 }}>
        <Typography variant="h6" gutterBottom>
          Upload
        </Typography>
        <input
          type="file"
          multiple
          accept=".pdf,.xlsx,.png,.jpg,.jpeg"
          data-testid="documents-file"
          onChange={(e) => void upload(e.target.files)}
        />
        <Typography variant="body2" sx={{ mt: 2 }} data-testid="documents-status">
          {status}
        </Typography>
      </Paper>

      <Paper sx={{ p: 3, mb: 3 }}>
        <Typography variant="h6" gutterBottom>
          Uploaded documents
        </Typography>
        {documents.length === 0 ? (
          <Typography variant="body2" data-testid="documents-empty">
            No documents yet - upload one above.
          </Typography>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>File name</TableCell>
                <TableCell>Media type</TableCell>
                <TableCell>Size</TableCell>
                <TableCell>SHA-256</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {documents.map((doc) => (
                <TableRow key={doc.id} data-testid="document-row" data-file-name={doc.fileName}>
                  <TableCell>{doc.fileName}</TableCell>
                  <TableCell>{doc.contentType}</TableCell>
                  <TableCell>{formatSize(doc.length)}</TableCell>
                  <TableCell>
                    <code>{doc.sha256.slice(0, 12)}…</code>
                  </TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={1}>
                      <Button size="small" onClick={() => void download(doc)} data-testid="document-download">
                        Download
                      </Button>
                      <Button size="small" onClick={() => void showPreview(doc)} data-testid="document-preview">
                        Preview
                      </Button>
                      <Button size="small" onClick={() => void showSummary(doc, false)} data-testid="document-summary">
                        Summary
                      </Button>
                      <Button size="small" onClick={() => void showSummary(doc, true)} data-testid="document-summary-pdf">
                        Summary as PDF
                      </Button>
                      <Button size="small" onClick={() => void copy(doc)} data-testid="document-copy">
                        Copy
                      </Button>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
        {copyStatus && (
          <Typography variant="body2" sx={{ mt: 2 }} data-testid="document-copy-status">
            {copyStatus}
          </Typography>
        )}
      </Paper>

      {preview && (
        <Paper sx={{ p: 3, mb: 3 }}>
          <Typography variant="h6" gutterBottom>
            Preview: {preview.fileName}
          </Typography>
          <img src={preview.url} alt={preview.fileName} style={{ maxWidth: '100%', maxHeight: 320 }} data-testid="document-preview-image" />
        </Paper>
      )}
      {previewMessage && (
        <Alert severity="info" sx={{ mb: 3 }} data-testid="document-preview-message">
          {previewMessage}
        </Alert>
      )}

      {summary && (
        <Paper sx={{ p: 3, mb: 3 }} data-testid="document-summary-json">
          <Typography variant="h6" gutterBottom>
            Summary (JSON)
          </Typography>
          <Typography variant="body2">
            File name: <span data-testid="summary-file-name">{summary.fileName}</span>
          </Typography>
          <Typography variant="body2">
            Kind: <span data-testid="summary-kind">{summary.kind}</span>
          </Typography>
          <Typography variant="body2">Media type: {summary.contentType}</Typography>
          <Typography variant="body2">Size: {formatSize(summary.length)}</Typography>
          <Typography variant="body2">
            SHA-256: <code>{summary.sha256}</code>
          </Typography>
          <Typography variant="body2">
            ETag: <code data-testid="summary-etag">{summaryETag}</code>
          </Typography>
          <Button size="small" variant="outlined" sx={{ mt: 1 }} onClick={() => void checkSummary()} data-testid="summary-check">
            Check for changes
          </Button>
          {summaryCheck && (
            <Typography variant="body2" sx={{ mt: 1 }} data-testid="summary-check-status">
              {summaryCheck}
            </Typography>
          )}
        </Paper>
      )}
    </Box>
  );
}