import { useState } from 'react';
import { Button, Paper, Typography } from '@mui/material';
import { apiBaseUrl, demoToken } from '../config/api';
import { isXmlMediaType, parseXml, readUser } from '../api/helpers/xml';
import type { User } from '../api/models';

/**
 * The user as XML: asked for with `Accept: application/xml` (the generated server negotiates by Accept), and read back
 * into the model by the generated `readUser` mapping.
 */
export default function UserXmlView({ userId }: { userId: string }) {
  const [xml, setXml] = useState<string | null>(null);
  const [fromXml, setFromXml] = useState<User | null>(null);

  const view = async () => {
    const response = await fetch(`${apiBaseUrl}/api/v1/users/${userId}`, {
      headers: { Accept: 'application/xml', Authorization: `Bearer ${demoToken}` },
    });
    const text = await response.text();
    setXml(text);
    setFromXml(response.ok && isXmlMediaType(response.headers.get('Content-Type')) ? readUser(parseXml(text)) : null);
  };

  return (
    <Paper sx={{ p: 3, mt: 3 }}>
      <Typography variant="h6" gutterBottom>
        As XML
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        The same user, asked for with <code>Accept: application/xml</code>: the generated server answers XML, and the
        generated <code>readUser</code> mapping reads it back into the model.
      </Typography>
      <Button variant="outlined" onClick={() => void view()} data-testid="user-view-xml">
        View as XML
      </Button>
      {xml !== null && (
        <>
          <Typography variant="body2" sx={{ mt: 2 }} data-testid="user-xml-parsed">
            Read back from the XML: {fromXml?.firstName} {fromXml?.lastName} · {fromXml?.email} · {fromXml?.address.city}
          </Typography>
          <pre style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-all', maxHeight: 240, overflow: 'auto' }} data-testid="user-xml">
            {xml}
          </pre>
        </>
      )}
    </Paper>
  );
}