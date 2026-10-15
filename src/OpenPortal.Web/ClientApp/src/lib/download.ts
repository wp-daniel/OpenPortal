import type { DownloadedFile } from '@/api/client'

/** Hands a file fetched by script to the browser's download (a temporary object URL, revoked right after). */
export function saveFile({ blob, fileName }: DownloadedFile): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}
