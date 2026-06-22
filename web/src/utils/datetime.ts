import type { Task } from '../types'

/** A task is overdue if it has a past due instant and isn't Done. Pure client-side derivation. */
export function isOverdue(task: Pick<Task, 'dueAt' | 'status'>): boolean {
  return !!task.dueAt && task.status !== 'Done' && new Date(task.dueAt).getTime() < Date.now()
}

/** Render a stored UTC instant in the browser's local timezone. */
export function formatDue(iso: string | null): string {
  if (!iso) return ''
  return new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

/** Date only (no time) — for created/updated stamps. */
export function formatDate(iso: string | null): string {
  if (!iso) return ''
  return new Date(iso).toLocaleDateString(undefined, { dateStyle: 'medium' })
}

/** ISO/UTC → the local wall-clock value an <input type="datetime-local"> expects. */
export function isoToLocalInput(iso: string | null): string {
  if (!iso) return ''
  const d = new Date(iso)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** A datetime-local value (local wall time) → ISO/UTC string for the API. Empty → null. */
export function localInputToIso(local: string): string | null {
  if (!local) return null
  return new Date(local).toISOString()
}
