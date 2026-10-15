import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { FileText, Loader2, LogOut } from 'lucide-react'
import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { authApi } from '@/api/auth'
import { legalApi, legalKeys } from '@/api/legal'
import type { LegalDocumentKind } from '@/api/types'
import { Markdown } from '@/components/Markdown'
import { Section } from '@/components/Section'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { ScrollArea } from '@/components/ui/scroll-area'
import { notify } from '@/hooks/useToast'
import { SESSION_QUERY_KEY, useSession } from '@/hooks/useSession'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { serverReturnUrl } from '@/lib/returnUrl'
import type { RedirectState } from '@/routes/guards'

const CARD = 'bg-card/80 shadow-lg backdrop-blur'

/** The privacy notice or the terms of use in force, for anyone (signed in or not). */
export function LegalDocumentPage({ kind }: { kind: LegalDocumentKind }) {
  const { t, language } = useI18n()

  const document = useQuery({
    queryKey: legalKeys.current(kind, language),
    queryFn: ({ signal }) => legalApi.current(kind, signal),
  })

  if (document.isPending) {
    return <LoadingState />
  }

  if (document.isError) {
    return (
      <ErrorPanel
        message={describeError(document.error)}
        traceId={traceIdOf(document.error)}
        onRetry={() => void document.refetch()}
      />
    )
  }

  if (!document.data) {
    return (
      <Section className={CARD}>
        <EmptyState title={t(`legal.${kind}.missing`)} description={t('legal.missingDescription')} />
      </Section>
    )
  }

  return (
    <Section
      className={CARD}
      title={
        <span className="flex items-center gap-2">
          <FileText className="size-5" />
          {document.data.title}
        </span>
      }
      description={t('legal.versionOf', {
        version: document.data.version,
        date: formatDate(document.data.publishedAtUtc, { dateStyle: 'long' }),
      })}
    >
      <Markdown>{document.data.body}</Markdown>
    </Section>
  )
}

/**
 * The versions in force the signed-in account has not accepted yet, shown before anything else in the portal (and
 * before another application's sign-in, which sends the user here with `returnUrl`).
 */
export function AcceptLegalPage() {
  const { t, language } = useI18n()
  const { user } = useSession()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const location = useLocation()
  const [accepted, setAccepted] = useState(false)

  const pending = user?.pendingLegalDocuments ?? []

  const documents = useQueries({
    queries: pending.map((document) => ({
      queryKey: legalKeys.current(document.kind, language),
      queryFn: ({ signal }: { signal: AbortSignal }) => legalApi.current(document.kind, signal),
    })),
  })

  const leave = () => {
    const back = serverReturnUrl(location.search)
    if (back) {
      window.location.replace(back)
      return
    }

    const from = (location.state as RedirectState | null)?.from
    navigate(from && !from.startsWith('/legal') ? from : '/', { replace: true })
  }

  const accept = useMutation({
    mutationFn: () => legalApi.accept(pending.map((document) => document.id)),
    meta: { handlesErrors: true },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })
      leave()
    },
    onError: async (error) => {
      // A newer version was published meanwhile: reload what is owed and show it.
      notify.fromError(error, t('legal.accept.failed'))
      setAccepted(false)
      await queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })
      await queryClient.invalidateQueries({ queryKey: legalKeys.all })
    },
  })

  const signOut = useMutation({
    mutationFn: () => authApi.logout(),
    onSettled: () => {
      queryClient.clear()
      navigate('/sign-in', { replace: true })
    },
  })

  if (pending.length === 0) {
    return (
      <Section className={CARD}>
        <div className="grid justify-items-center gap-4 py-2 text-center">
          <p className="text-sm">{t('legal.accept.nothing')}</p>
          <Button onClick={leave}>{t('legal.accept.continue')}</Button>
        </div>
      </Section>
    )
  }

  const loading = documents.some((document) => document.isPending)

  return (
    <Section className={CARD} title={t('legal.accept.title')} description={t('legal.accept.description')}>
      <div className="grid gap-6">
        {loading && <LoadingState />}
        {documents.map((query, index) => {
          const document = query.data
          if (!document) {
            return null
          }

          return (
            <article key={pending[index]!.id} className="grid gap-2">
              <h2 className="flex items-baseline justify-between gap-3 font-semibold">
                {document.title}
                <Link to={`/${document.kind}`} target="_blank" className="text-muted-foreground text-xs font-normal underline">
                  {t('legal.accept.openFull')}
                </Link>
              </h2>
              <ScrollArea className="bg-background/60 h-64 rounded-md border">
                <Markdown className="p-4">{document.body}</Markdown>
              </ScrollArea>
            </article>
          )
        })}

        <div className="flex items-start gap-3">
          <Checkbox
            id="legal-accept"
            className="mt-0.5"
            checked={accepted}
            onCheckedChange={(checked) => setAccepted(checked === true)}
          />
          <label htmlFor="legal-accept" className="text-sm leading-snug">
            {t('legal.accept.confirm')}
          </label>
        </div>

        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-between">
          <Button variant="ghost" onClick={() => signOut.mutate()} disabled={signOut.isPending}>
            <LogOut />
            {t('nav.signOut')}
          </Button>
          <Button onClick={() => accept.mutate()} disabled={!accepted || loading || accept.isPending}>
            {accept.isPending && <Loader2 className="animate-spin" />}
            {t('legal.accept.submit')}
          </Button>
        </div>
      </div>
    </Section>
  )
}
