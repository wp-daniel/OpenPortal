/** Case- and accent-insensitive substring test shared by the client-side table filters. */
export function matchesSearch(search: string, ...fields: readonly (string | null | undefined)[]): boolean {
  const needle = normalise(search)

  return needle === '' || fields.some((field) => field != null && normalise(field).includes(needle))
}

function normalise(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .trim()
}
