/** Accepted input formats for profile photos (validated again by the API). */
export const AVATAR_ACCEPT = 'image/png,image/jpeg,image/webp'
/** Largest original file we try to process in the browser. */
export const AVATAR_MAX_INPUT_BYTES = 15 * 1024 * 1024
/** Edge of the square photo sent to the API: crisp on tokens and retina avatars, still tiny. */
export const AVATAR_SIZE = 256

function loadImage(file: Blob) {
  return new Promise<HTMLImageElement>((resolve, reject) => {
    const url = URL.createObjectURL(file)
    const image = new Image()
    image.onload = () => { URL.revokeObjectURL(url); resolve(image) }
    image.onerror = () => { URL.revokeObjectURL(url); reject(new Error('Não foi possível ler esta imagem.')) }
    image.src = url
  })
}

function toBlob(canvas: HTMLCanvasElement, type: string, quality: number) {
  return new Promise<Blob | null>(resolve => canvas.toBlob(resolve, type, quality))
}

/**
 * Center-crops the photo to a square and scales it down to {@link AVATAR_SIZE}, so any
 * proportion works as a round avatar/token and uploads stay small (well under the 1 MB limit).
 * Prefers WebP; falls back to JPEG where the browser cannot encode WebP.
 */
export async function prepareAvatar(file: File): Promise<{ blob: Blob; fileName: string }> {
  if (!AVATAR_ACCEPT.split(',').includes(file.type)) throw new Error('Use uma foto PNG, JPEG ou WebP.')
  if (file.size > AVATAR_MAX_INPUT_BYTES) throw new Error('A foto deve ter no máximo 15 MB.')

  const image = await loadImage(file)
  const side = Math.min(image.naturalWidth, image.naturalHeight)
  if (side < 1) throw new Error('Não foi possível ler esta imagem.')
  const size = Math.min(AVATAR_SIZE, side)
  const canvas = document.createElement('canvas')
  canvas.width = size
  canvas.height = size
  const context = canvas.getContext('2d')
  if (!context) throw new Error('Seu navegador não conseguiu processar a foto.')
  context.imageSmoothingQuality = 'high'
  context.drawImage(
    image,
    (image.naturalWidth - side) / 2, (image.naturalHeight - side) / 2, side, side,
    0, 0, size, size)

  const webp = await toBlob(canvas, 'image/webp', .88)
  if (webp && webp.type === 'image/webp') return { blob: webp, fileName: 'avatar.webp' }
  const jpeg = await toBlob(canvas, 'image/jpeg', .9)
  if (!jpeg) throw new Error('Seu navegador não conseguiu processar a foto.')
  return { blob: jpeg, fileName: 'avatar.jpg' }
}
