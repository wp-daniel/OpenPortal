import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Mail } from 'lucide-react'
import { notificationsApi } from '@/api/notifications'
import type { NotificationEmail } from '@/api/types'
import { Section } from '@/components/Section'
import { ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { notify } from '@/hooks/useToast'
import { useI18n } from '@/i18n/useI18n'
import { describeError } from '@/lib/errors'

const PREFERENCES_KEY = ['notification-preferences'] as const
const OPTIONS: readonly NotificationEmail[] = ['security', 'instant', 'daily', 'off']

/** Which notifications also arrive by email; each choice is saved as it is made. */
export function NotificationEmailPreferences() {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const preferences = useQuery({ queryKey: PREFERENCES_KEY, queryFn: ({ signal }) => notificationsApi.preferences(signal) })

  const save = useMutation({
    mutationFn: (email: NotificationEmail) => notificationsApi.updatePreferences(email),
    onSuccess: (saved) => {
      queryClient.setQueryData(PREFERENCES_KEY, saved)
      notify.success(t('notifications.email.saved'))
    },
  })

  return (
    <Section
      title={
        <span className="flex items-center gap-2">
          <Mail className="size-5" />
          {t('notifications.email.title')}
        </span>
      }
      description={t('notifications.email.description')}
    >
      {preferences.isPending && <LoadingState />}
      {preferences.isError && <ErrorPanel message={describeError(preferences.error)} onRetry={() => void preferences.refetch()} />}
      {preferences.data && (
        <div className="grid max-w-2xl gap-4">
          {!preferences.data.emailAvailable && (
            <Alert>
              <AlertDescription>{t('notifications.email.unavailable')}</AlertDescription>
            </Alert>
          )}
          <RadioGroup
            value={preferences.data.email}
            onValueChange={(value) => save.mutate(value as NotificationEmail)}
            disabled={save.isPending}
            className="gap-3"
          >
            {OPTIONS.map((option) => (
              <div key={option} className="flex items-start gap-3">
                <RadioGroupItem value={option} id={`email-${option}`} className="mt-0.5" />
                <div className="grid gap-0.5">
                  <Label htmlFor={`email-${option}`}>{t(`notifications.email.option.${option}`)}</Label>
                  <p className="text-muted-foreground text-xs">{t(`notifications.email.option.${option}Hint`)}</p>
                </div>
              </div>
            ))}
          </RadioGroup>
        </div>
      )}
    </Section>
  )
}
