import { useState } from 'react';
import { Button, Paper, Typography } from '@mui/material';
import { apiBaseUrl, demoToken } from '../config/api';
import { writeCreateUserRequestList } from '../api/helpers/xml';
import type { CreateUserRequest } from '../api/models';

// Indents an XML document, one element per line - for reading in the text area
function indentXml(xml: string): string {
  let depth = 0;
  return xml
    .replace(/></g, '>\n<')
    .split('\n')
    .map((line) => {
      if (line.startsWith('</')) {
        depth--;
      }

      const indented = '  '.repeat(Math.max(depth, 0)) + line;
      if (!line.startsWith('</') && !line.endsWith('/>') && !/<\/[^>]+>$/.test(line)) {
        depth++;
      }

      return indented;
    })
    .join('\n');
}

// Two users with unique emails and surnames, so every import creates new users (the server refuses a second user with
// the same name, an item of a list as well as a single user)
function sampleXml(): string {
  const stamp = Date.now();
  const suffix = `${stamp.toString(36).slice(-4)}${Math.floor(Math.random() * 1296).toString(36)}`;
  const users: CreateUserRequest[] = [
    {
      firstName: 'Ada',
      lastName: `Lovelace-${suffix}`,
      email: `ada.${stamp}@example.com`,
      dateOfBirth: '1815-12-10',
      role: 'Admin',
      address: { street: "12 St James's Square", city: 'London', postalCode: 'SW1Y 4JH', country: 'England', countryCode: 'GB' },
    },
    {
      firstName: 'Grace',
      lastName: `Hopper-${suffix}`,
      email: `grace.${stamp}@example.com`,
      dateOfBirth: '1906-12-09',
      role: 'Manager',
      address: { street: '1 Navy Yard', city: 'Arlington', postalCode: '22201', country: 'United States', countryCode: 'US' },
    },
  ];
  return indentXml(writeCreateUserRequestList(users, 'users'));
}

// A ProblemDetails as one line: each validation error's key and first message, else the title
function problemText(problem: { title?: string; errors?: Record<string, string[]> }): string {
  return problem.errors
    ? Object.entries(problem.errors).map(([key, messages]) => `${key}: ${messages[0] ?? ''}`).join('; ')
    : problem.title ?? '';
}

/**
 * Imports a list of users as one XML document: the generated `writeCreateUserRequestList` writes it, and the generated
 * server binds the list by its Content-Type (JSON or XML) and validates every user.
 */
export default function UsersImport() {
  const [xml, setXml] = useState(sampleXml);
  const [status, setStatus] = useState<string | null>(null);
  const [imported, setImported] = useState<string[]>([]);
  const [isImporting, setIsImporting] = useState(false);

  const importUsers = async () => {
    setIsImporting(true);
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/users/import`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/xml', Authorization: `Bearer ${demoToken}` },
        body: xml,
      });
      const body = await response.json();
      if (response.ok) {
        const users = (body as { firstName: string; lastName: string; email: string }[]).map(
          (u) => `${u.firstName} ${u.lastName} · ${u.email}`,
        );
        setImported(users);
        setStatus(`Imported ${users.length} users from XML`);
      } else {
        setImported([]);
        setStatus(`${response.status}: ${problemText(body)}`);
      }
    } catch (error) {
      setImported([]);
      setStatus(`Import failed: ${error instanceof Error ? error.message : String(error)}`);
    } finally {
      setIsImporting(false);
    }
  };

  return (
    <Paper sx={{ p: 3, mt: 3 }}>
      <Typography variant="h6" gutterBottom>
        Import users as XML
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        A list of users as one XML document, written by the generated <code>writeCreateUserRequestList</code>.{' '}
        <code>POST /users/import</code> takes the list as JSON or XML: the generated server binds it by its Content-Type,
        and the validation filter checks every user - make a first name one letter long to see it.
      </Typography>
      <textarea
        value={xml}
        onChange={(e) => setXml(e.target.value)}
        rows={12}
        spellCheck={false}
        data-testid="users-import-xml"
        style={{ width: '100%', fontFamily: 'monospace', fontSize: '0.8rem' }}
      />
      <Button variant="contained" sx={{ mt: 1 }} disabled={isImporting} onClick={() => void importUsers()} data-testid="users-import">
        Import
      </Button>
      {status !== null && (
        <Typography variant="body2" sx={{ mt: 2 }} data-testid="users-import-status">
          {status}
        </Typography>
      )}
      {imported.map((user) => (
        <Typography key={user} variant="body2" data-testid="users-import-row">
          {user}
        </Typography>
      ))}
    </Paper>
  );
}
