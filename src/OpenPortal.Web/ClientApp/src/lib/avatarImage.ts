/** The formats the server accepts (it checks the bytes, so this is for the file picker and early feedback). */
export const AVATAR_ACCEPT = 'image/png,image/jpeg,image/webp'

/** Side of the square the picture is scaled to before upload: twice the largest size it is shown at. */
const AVATAR_SIZE = 256

/** The server's limit on the stored image (`UserAvatar.MaxBytes`). */
const MAX_UPLOAD_BYTES = 256 * 1024

/** Larger sources are refused before decoding, so a huge camera file cannot stall the tab. */
const MAX_SOURCE_BYTES = 15 * 1024 * 1024

export type AvatarImageProblem = 'type' | 'size' | 'decode'

export class AvatarImageError extends Error {
  readonly problem: AvatarImageProblem

  constructor(problem: AvatarImageProblem) {
    super(`avatar image: ${problem}`)
    this.name = 'AvatarImageError'
    this.problem = problem
  }
}

/**
 * Turns a picked file into the image that is uploaded: centre-cropped to a square and scaled to
 * {@link AVATAR_SIZE}, as WebP where the browser can encode it and JPEG otherwise (Safari cannot encode WebP).
 * Doing this here keeps uploads small whatever the source, and strips the source's metadata (EXIF location
 * included), because only the pixels are redrawn.
 */
export async function prepareAvatar(file: File): Promise<Blob> {
  if (!AVATAR_ACCEPT.split(',').includes(file.type)) {
    throw new AvatarImageError('type')
  }

  if (file.size > MAX_SOURCE_BYTES) {
    throw new AvatarImageError('size')
  }

  let bitmap: ImageBitmap

  try {
    // Honours the EXIF orientation, so a portrait phone photo is not drawn sideways.
    bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' })
  } catch {
    throw new AvatarImageError('decode')
  }

  try {
    const side = Math.min(bitmap.width, bitmap.height)
    const canvas = document.createElement('canvas')
    canvas.width = AVATAR_SIZE
    canvas.height = AVATAR_SIZE

    const context = canvas.getContext('2d')
    if (!context) {
      throw new AvatarImageError('decode')
    }

    context.imageSmoothingQuality = 'high'
    context.drawImage(
      bitmap,
      (bitmap.width - side) / 2,
      (bitmap.height - side) / 2,
      side,
      side,
      0,
      0,
      AVATAR_SIZE,
      AVATAR_SIZE,
    )

    let blob = await toBlob(canvas, 'image/webp')
    // An unsupported type makes toBlob fall back to PNG, which is much larger for a photo.
    if (!blob || blob.type !== 'image/webp') {
      blob = await toBlob(canvas, 'image/jpeg')
    }

    if (!blob) {
      throw new AvatarImageError('decode')
    }

    if (blob.size > MAX_UPLOAD_BYTES) {
      throw new AvatarImageError('size')
    }

    return blob
  } finally {
    bitmap.close()
  }
}

function toBlob(canvas: HTMLCanvasElement, type: string): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob(resolve, type, 0.9))
}
