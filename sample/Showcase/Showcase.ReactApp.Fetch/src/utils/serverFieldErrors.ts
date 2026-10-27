import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';

/**
 * Puts the errors of a 400 (keyed by the wire names the client sent: `firstName`, `address.city`) on the form's own
 * fields - the form is flat, so a nested key goes to the field with its last name. The specification's errors and the
 * domain's come the same way. Returns the messages no field shows, for a summary.
 */
export function applyServerErrors<T extends FieldValues>(
  errors: Record<string, string[]>,
  fields: readonly string[],
  setError: UseFormSetError<T>,
): string[] {
  const unmatched: string[] = [];
  for (const [key, messages] of Object.entries(errors)) {
    const name = key.slice(key.lastIndexOf('.') + 1);
    if (fields.includes(name)) {
      setError(name as Path<T>, { type: 'server', message: messages.join(' ') });
    } else {
      unmatched.push(...messages);
    }
  }
  return unmatched;
}
