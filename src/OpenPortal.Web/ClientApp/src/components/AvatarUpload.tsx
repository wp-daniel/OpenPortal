import { useMutation } from '@tanstack/react-query'
import { ImageUp, Loader2, Trash2 } from 'lucide-react'
import { useEffect, useId, useRef, useState, type DragEvent } from 'react'
import { type AvatarUser, UserAvatar } from '@/components/UserAvatar'
import { Button } from '@/components/ui/button'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { AVATAR_ACCEPT, AvatarImageError, prepareAvatar } from '@/lib/avatarImage'
import { reportFormError } from '@/lib/forms'

/**
 * Picks, previews, uploads and removes a profile picture. A file can be chosen with the button or dropped
 * on the zone; it is cropped and scaled in the browser (`prepareAvatar`) before `upload` sends it.
 *
 * The preview switches to the uploaded image as soon as the server accepts it, without waiting for the
 * caller's queries to refresh; `onChanged` is where the caller invalidates them.
 */
export function AvatarUpload({
  user,
  upload,
  remove,
  onChanged,
}: {
  user: AvatarUser
  upload: (image: Blob) => Promise<void>
  remove: () => Promise<void>
  onChanged?: () => void
}) {
  const { t } = useI18n()
  const inputId = useId()
  const hintId = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const objectUrl = useRef<string | null>(null)
  // undefined: show what the server has; null: removed here; string: uploaded here.
  const [preview, setPreview] = useState<string | null | undefined>(undefined)
  const [problem, setProblem] = useState<string>()
  const [dragging, setDragging] = useState(false)

  useEffect(() => () => {
    if (objectUrl.current) {
      URL.revokeObjectURL(objectUrl.current)
    }
  }, [])

  function showPreview(next: Blob | null) {
    if (objectUrl.current) {
      URL.revokeObjectURL(objectUrl.current)
    }

    objectUrl.current = next ? URL.createObjectURL(next) : null
    setPreview(objectUrl.current)
  }

  const save = useMutation({
    mutationFn: async (file: File) => {
      const image = await prepareAvatar(file)
      await upload(image)

      return image
    },
    meta: { handlesErrors: true },
    onSuccess: (image) => {
      showPreview(image)
      notify.success(t('avatar.saved'))
      onChanged?.()
    },
    onError: (failure) => {
      if (failure instanceof AvatarImageError) {
        setProblem(t(`avatar.error.${failure.problem}`))
      } else {
        reportFormError(failure, t('avatar.failed'))
      }
    },
  })

  const clear = useMutation({
    mutationFn: remove,
    meta: { handlesErrors: true },
    onSuccess: () => {
      showPreview(null)
      notify.success(t('avatar.removed'))
      onChanged?.()
    },
    onError: (failure) => reportFormError(failure, t('avatar.failed')),
  })

  const busy = save.isPending || clear.isPending
  const hasPicture = preview === undefined ? user.avatarUpdatedAtUtc !== null : preview !== null

  function choose(file: File | undefined) {
    if (!file || busy) {
      return
    }

    setProblem(undefined)
    save.mutate(file)
  }

  function onDragOver(event: DragEvent) {
    event.preventDefault()
    event.dataTransfer.dropEffect = 'copy'
    setDragging(true)
  }

  function onDragLeave(event: DragEvent) {
    // Moving onto a child fires dragleave on the zone too; only leaving the zone itself counts.
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
      setDragging(false)
    }
  }

  function onDrop(event: DragEvent) {
    event.preventDefault()
    setDragging(false)
    choose(event.dataTransfer.files[0])
  }

  return (
    <div className="flex flex-col items-start gap-4 sm:flex-row sm:items-center">
      <UserAvatar user={user} src={preview} className="size-20 text-lg" />

      <div
        onDragOver={onDragOver}
        onDragLeave={onDragLeave}
        onDrop={onDrop}
        data-dragging={dragging || undefined}
        className="border-input data-[dragging]:border-ring data-[dragging]:bg-accent/50 grid w-full flex-1 gap-3 rounded-lg border border-dashed p-4 transition-colors"
      >
        <div className="space-y-1">
          <label htmlFor={inputId} className="text-sm font-medium">
            {t('avatar.title')}
          </label>
          <p id={hintId} className="text-muted-foreground text-xs">
            {t('avatar.hint')}
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button size="sm" variant="outline" disabled={busy} onClick={() => inputRef.current?.click()}>
            {save.isPending ? <Loader2 className="animate-spin" /> : <ImageUp />}
            {save.isPending ? t('avatar.uploading') : hasPicture ? t('avatar.replace') : t('avatar.choose')}
          </Button>
          {hasPicture && (
            <Button size="sm" variant="ghost" disabled={busy} onClick={() => clear.mutate()}>
              {clear.isPending ? <Loader2 className="animate-spin" /> : <Trash2 />}
              {t('avatar.remove')}
            </Button>
          )}
        </div>

        <input
          ref={inputRef}
          id={inputId}
          type="file"
          accept={AVATAR_ACCEPT}
          aria-describedby={hintId}
          className="sr-only"
          tabIndex={-1}
          onChange={(event) => {
            choose(event.target.files?.[0])
            // Cleared so picking the same file again still fires a change.
            event.target.value = ''
          }}
        />

        {problem && (
          <p role="alert" className="text-destructive text-sm">
            {problem}
          </p>
        )}
      </div>
    </div>
  )
}
