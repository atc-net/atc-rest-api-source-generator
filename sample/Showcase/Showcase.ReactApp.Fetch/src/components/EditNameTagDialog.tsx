import { useEffect, useState } from 'react';
import { Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material';

interface EditNameTagDialogProps {
  open: boolean;
  title: string;
  name: string;
  tag?: string | null;
  saving: boolean;
  onSave: (name: string, tag: string) => void;
  onCancel: () => void;
}

// Edits the name and the tag of an account or a task
export function EditNameTagDialog({ open, title, name, tag, saving, onSave, onCancel }: EditNameTagDialogProps) {
  const [editName, setEditName] = useState(name);
  const [editTag, setEditTag] = useState(tag ?? '');

  useEffect(() => {
    setEditName(name);
    setEditTag(tag ?? '');
  }, [name, tag, open]);

  return (
    <Dialog open={open} onClose={onCancel}>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <TextField label="New name" size="small" value={editName} onChange={(e) => setEditName(e.target.value)} />
          <TextField label="New tag" size="small" value={editTag} onChange={(e) => setEditTag(e.target.value)} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel}>Cancel</Button>
        <Button
          variant="contained"
          disabled={saving || !editName.trim()}
          onClick={() => onSave(editName.trim(), editTag.trim())}
        >
          Save
        </Button>
      </DialogActions>
    </Dialog>
  );
}
