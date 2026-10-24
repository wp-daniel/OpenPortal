import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, Check, ClipboardCheck, Loader2, Plus, Undo2, Users, UserRound, X } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { accessKeys, applicationsApi } from '@/api/access'
import { reviewKeys, reviewsApi, type AccessReview, type AccessReviewDecision, type AccessReviewStatus } from '@/api/reviews'
import { FormField } from '@/components/FormField'
import { PageHeader } from '@/components/PageHeader'
import { Section } from '@/components/Section'
import { EmptyState, ErrorPanel, LoadingState } from '@/components/StatePanels'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { notify } from '@/hooks/useToast'
import { formatDate } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { useAccessRefresh } from '@/lib/access'
import { fromLocalInput } from '@/lib/dateInput'
import { describeError, traceIdOf } from '@/lib/errors'
import { reportFormError } from '@/lib/forms'

const STATUS_VARIANT: Record<AccessReviewStatus, 'warning' | 'success' | 'secondary'> = {
  open: 'warning',
  completed: 'success',
  cancelled: 'secondary',
}

/**
 * Access reviews (recertification): a snapshot of who can open an application, each grant confirmed or revoked by a
 * reviewer; completing the review applies the revocations.
 */
export function AdminAccessReviewsPage() {
  const { reviewId } = useParams()

  return reviewId ? <ReviewDetail reviewId={reviewId} /> : <ReviewList />
}

function ReviewList() {
  const { t } = useI18n()
  const [starting, setStarting] = useState(false)
  const reviews = useQuery({ queryKey: reviewKeys.all, queryFn: ({ signal }) => reviewsApi.list(signal) })

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('reviews.title')}
        description={t('reviews.description')}
        actions={
          <Button onClick={() => setStarting(true)}>
            <Plus />
            {t('reviews.start')}
          </Button>
        }
      />

      <Section>
        {reviews.isPending && <LoadingState />}
        {reviews.isError && <ErrorPanel message={describeError(reviews.error)} traceId={traceIdOf(reviews.error)} onRetry={() => void reviews.refetch()} />}
        {reviews.data && reviews.data.length === 0 && <EmptyState title={t('reviews.empty')} description={t('reviews.emptyDescription')} />}
        {reviews.data && reviews.data.length > 0 && (
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('reviews.column.review')}</TableHead>
                  <TableHead>{t('reviews.column.application')}</TableHead>
                  <TableHead>{t('common.status')}</TableHead>
                  <TableHead className="text-right">{t('reviews.column.progress')}</TableHead>
                  <TableHead>{t('reviews.column.due')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {reviews.data.map((review) => (
                  <TableRow key={review.id}>
                    <TableCell>
                      <Link to={`/admin/access-reviews/${review.id}`} className="font-medium underline-offset-2 hover:underline">
                        {review.name}
                      </Link>
                    </TableCell>
                    <TableCell>{review.applicationName}</TableCell>
                    <TableCell>
                      <Badge variant={STATUS_VARIANT[review.status]}>{t(`reviews.status.${review.status}`)}</Badge>
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {review.decidedCount}/{review.itemCount}
                    </TableCell>
                    <TableCell className="whitespace-nowrap">{review.dueAtUtc ? formatDate(review.dueAtUtc, { dateStyle: 'medium' }) : '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </Section>

      <Dialog open={starting} onOpenChange={setStarting}>
        <DialogContent>{starting && <StartReviewForm onDone={() => setStarting(false)} />}</DialogContent>
      </Dialog>
    </div>
  )
}

function StartReviewForm({ onDone }: { onDone: () => void }) {
  const { t } = useI18n()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [applicationId, setApplicationId] = useState('')
  const [name, setName] = useState('')
  const [due, setDue] = useState('')
  const [revokeUndecided, setRevokeUndecided] = useState(false)

  const applications = useQuery({ queryKey: accessKeys.applications, queryFn: ({ signal }) => applicationsApi.list(signal) })

  const start = useMutation({
    mutationFn: () =>
      reviewsApi.start({ applicationId, name: name.trim() || null, dueAtUtc: fromLocalInput(due), revokeUndecided }),
    meta: { handlesErrors: true },
    onSuccess: async (review) => {
      await queryClient.invalidateQueries({ queryKey: reviewKeys.all })
      onDone()
      navigate(`/admin/access-reviews/${review.review.id}`)
    },
    onError: (failure) => reportFormError(failure, t('reviews.startFailed')),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (applicationId) start.mutate()
  }

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      <DialogHeader>
        <DialogTitle>{t('reviews.startTitle')}</DialogTitle>
        <DialogDescription>{t('reviews.startDescription')}</DialogDescription>
      </DialogHeader>

      <fieldset className="grid gap-2">
        <legend className="mb-1 text-sm font-medium">{t('reviews.column.application')}</legend>
        {applications.isPending && <LoadingState />}
        <RadioGroup value={applicationId} onValueChange={setApplicationId} className="max-h-56 gap-2 overflow-y-auto">
          {applications.data
            ?.filter((application) => application.status === 'active')
            .map((application) => (
              <div key={application.id} className="flex items-center gap-2">
                <RadioGroupItem value={application.id} id={`review-app-${application.id}`} />
                <Label htmlFor={`review-app-${application.id}`} className="font-normal">
                  {application.displayName}
                  <span className="text-muted-foreground ml-1 text-xs">
                    ({t('reviews.grants', { count: application.userCount + application.groupCount })})
                  </span>
                </Label>
              </div>
            ))}
        </RadioGroup>
      </fieldset>

      <FormField label={t('reviews.name')} htmlFor="review-name" hint={t('reviews.nameHint')}>
        <Input value={name} maxLength={120} onChange={(event) => setName(event.target.value)} />
      </FormField>

      <FormField label={t('reviews.due')} htmlFor="review-due">
        <Input type="datetime-local" value={due} onChange={(event) => setDue(event.target.value)} />
      </FormField>

      <div className="flex items-start gap-2">
        <Checkbox id="review-revoke-undecided" checked={revokeUndecided} onCheckedChange={(checked) => setRevokeUndecided(checked === true)} className="mt-0.5" />
        <div className="grid gap-0.5">
          <Label htmlFor="review-revoke-undecided">{t('reviews.revokeUndecided')}</Label>
          <p className="text-muted-foreground text-xs">{t('reviews.revokeUndecidedHint')}</p>
        </div>
      </div>

      <DialogFooter>
        <Button variant="outline" onClick={onDone}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" disabled={!applicationId || start.isPending}>
          {start.isPending && <Loader2 className="animate-spin" />}
          {t('reviews.start')}
        </Button>
      </DialogFooter>
    </form>
  )
}

function ReviewDetail({ reviewId }: { reviewId: string }) {
  const { t } = useI18n()
  const queryClient = useQueryClient()
  const [confirming, setConfirming] = useState<'complete' | 'cancel' | null>(null)
  const refresh = useAccessRefresh()
  const review = useQuery({ queryKey: reviewKeys.one(reviewId), queryFn: ({ signal }) => reviewsApi.get(reviewId, signal) })

  const update = (data: AccessReview) => {
    queryClient.setQueryData(reviewKeys.one(reviewId), data)
    void queryClient.invalidateQueries({ queryKey: reviewKeys.all, exact: true })
  }

  const decide = useMutation({
    mutationFn: ({ itemId, decision }: { itemId: string; decision: AccessReviewDecision }) => reviewsApi.decide(reviewId, itemId, decision),
    onSuccess: update,
  })

  const close = useMutation({
    mutationFn: (action: 'complete' | 'cancel') => (action === 'complete' ? reviewsApi.complete(reviewId) : reviewsApi.cancel(reviewId)),
    onSuccess: async (data, action) => {
      setConfirming(null)
      update(data)
      await refresh()
      notify.success(action === 'complete' ? t('reviews.completed') : t('reviews.cancelled'))
    },
  })

  if (review.isPending) return <LoadingState />
  if (review.isError) return <ErrorPanel message={describeError(review.error)} traceId={traceIdOf(review.error)} onRetry={() => void review.refetch()} />

  const { review: summary, items } = review.data
  const open = summary.status === 'open'
  const toRevoke = items.filter((item) => item.decision === 'revoke' || (summary.revokeUndecided && item.decision === 'pending')).length

  return (
    <div className="space-y-6">
      <PageHeader
        title={summary.name}
        description={t('reviews.detailDescription', { application: summary.applicationName })}
        actions={
          open ? (
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => setConfirming('cancel')}>
                <Ban />
                {t('reviews.cancel')}
              </Button>
              <Button onClick={() => setConfirming('complete')}>
                <ClipboardCheck />
                {t('reviews.complete')}
              </Button>
            </div>
          ) : (
            <Badge variant={STATUS_VARIANT[summary.status]}>{t(`reviews.status.${summary.status}`)}</Badge>
          )
        }
      />

      <Section
        title={t('reviews.progress', { decided: summary.decidedCount, total: summary.itemCount })}
        description={summary.revokeUndecided ? t('reviews.revokeUndecidedHint') : undefined}
      >
        <div className="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('reviews.column.who')}</TableHead>
                <TableHead>{t('reviews.column.roles')}</TableHead>
                <TableHead>{t('reviews.column.decision')}</TableHead>
                <TableHead className="text-right">{t('common.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.map((item) => (
                <TableRow key={item.id}>
                  <TableCell>
                    <span className="flex items-center gap-2">
                      {item.subjectType === 'group' ? <Users className="text-muted-foreground size-4" /> : <UserRound className="text-muted-foreground size-4" />}
                      {item.subjectLabel}
                    </span>
                  </TableCell>
                  <TableCell className="text-xs">{item.roles.join(', ') || '—'}</TableCell>
                  <TableCell>
                    <Badge variant={item.decision === 'keep' ? 'success' : item.decision === 'revoke' ? 'destructive' : 'secondary'}>
                      {t(`reviews.decision.${item.decision}`)}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-right whitespace-nowrap">
                    {open && (
                      <>
                        <Button size="sm" variant={item.decision === 'keep' ? 'default' : 'ghost'} disabled={decide.isPending} onClick={() => decide.mutate({ itemId: item.id, decision: 'keep' })}>
                          <Check />
                          {t('reviews.keep')}
                        </Button>
                        <Button size="sm" variant={item.decision === 'revoke' ? 'destructive' : 'ghost'} disabled={decide.isPending} onClick={() => decide.mutate({ itemId: item.id, decision: 'revoke' })}>
                          <X />
                          {t('reviews.revoke')}
                        </Button>
                        {item.decision !== 'pending' && (
                          <Button size="sm" variant="ghost" aria-label={t('reviews.undo')} disabled={decide.isPending} onClick={() => decide.mutate({ itemId: item.id, decision: 'pending' })}>
                            <Undo2 />
                          </Button>
                        )}
                      </>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      </Section>

      <AlertDialog open={confirming !== null} onOpenChange={(value) => !value && setConfirming(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{confirming === 'complete' ? t('reviews.completeTitle') : t('reviews.cancelTitle')}</AlertDialogTitle>
            <AlertDialogDescription>
              {confirming === 'complete' ? t('reviews.completeDescription', { count: toRevoke }) : t('reviews.cancelDescription')}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              onClick={(event) => {
                event.preventDefault()
                if (confirming) close.mutate(confirming)
              }}
              disabled={close.isPending}
            >
              {close.isPending && <Loader2 className="animate-spin" />}
              {t('common.confirm')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
