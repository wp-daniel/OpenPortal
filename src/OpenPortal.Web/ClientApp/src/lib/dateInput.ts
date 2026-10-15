/** `<input type="datetime-local">` speaks local wall-clock time without a zone; the API speaks UTC ISO strings. */

const pad = (value: number) => String(value).padStart(2, '0')

/** An ISO instant as the input's `yyyy-MM-ddTHH:mm` in the browser's time zone, or '' for none. */
export function toLocalInput(iso: string | null | undefined): string {
  if (!iso) {
    return ''
  }

  const date = new Date(iso)
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

/** The input's value as a UTC ISO instant, or null when empty or not a date. */
export function fromLocalInput(value: string): string | null {
  if (!value) {
    return null
  }

  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date.toISOString()
}
