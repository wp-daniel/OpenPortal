import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, Plus } from 'lucide-react'
import { useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import {
  contentAdminApi,
  projectToForm,
  toProjectRequest,
  type ProjectForm as ProjectFields,
} from '@/api/content'
import type { ManagedProject } from '@/api/types'
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
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Textarea } from '@/components/ui/textarea'
import type { TFunction } from '@/i18n/store'
import { useI18n } from '@/i18n/useI18n'
import { describeError, traceIdOf } from '@/lib/errors'
import { notify } from '@/hooks/useToast'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'

const makeSchema = (t: TFunction) =>
  z
    .object({
      name: z.string().trim().min(1, t('validation.nameRequired')).max(160, t('validation.maxLength', { max: 160 })),
      // Only shape is checked here. The server owns slug normalisation and reports an invalid slug itself, so a
      // second slug regex in the browser could only disagree with the authoritative one.
      slug: z
        .string()
        .trim()
        .min(1, t('validation.slugRequired'))
        .max(160, t('validation.maxLength', { max: 160 }))
        .regex(/^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$/, t('validation.slugFormat')),
      summary: z.string().max(400, t('validation.maxLength', { max: 400 })),
      description: z.string().max(8_000, t('validation.maxLength', { max: 8000 })),
      url: z.union([z.literal(''), z.string().url(t('validation.absoluteUrl'))]),
      repositoryUrl: z.union([z.literal(''), z.string().url(t('validation.absoluteUrl'))]),
      position: z.union([z.literal(''), z.string().regex(/^\d+$/, t('validation.wholeNumber'))]),
      startedOn: z.union([z.literal(''), z.string().regex(/^\d{4}-\d{2}-\d{2}$/, t('validation.datePicker'))]),
      completedOn: z.union([z.literal(''), z.string().regex(/^\d{4}-\d{2}-\d{2}$/, t('validation.datePicker'))]),
    })
    .refine((values) => !values.startedOn || !values.completedOn || values.completedOn >= values.startedOn, {
      message: t('validation.completionBeforeStart'),
      path: ['completedOn'],
    })

type TextField = Exclude<keyof ProjectFields, 'id' | 'isPublished'>

/** The project editor: lists every project and creates or edits the one named in the route. */
export function ContentProjectPage() {
  const { projectId } = useParams<{ projectId: string }>()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const { t } = useI18n()
  const [pendingDelete, setPendingDelete] = useState<ManagedProject | null>(null)

  const projects = useQuery({
    queryKey: ['manage-projects'],
    queryFn: ({ signal }) => contentAdminApi.listProjects(signal),
  })

  // Failures on both mutations are toasted by the global MutationCache handler.
  const setPublished = useMutation({
    mutationFn: ({ project, published }: { project: ManagedProject; published: boolean }) =>
      contentAdminApi.setPublished(project.id, published),
    onSuccess: async (_result, { project, published }) => {
      notify.success(published ? t('projects.nowPublished') : t('projects.nowUnpublished'), project.name)
      await queryClient.invalidateQueries({ queryKey: ['manage-projects'] })
    },
  })

  const remove = useMutation({
    mutationFn: (project: ManagedProject) => contentAdminApi.deleteProject(project.id),
    onSuccess: async (_result, project) => {
      notify.success(t('projects.deleted'), project.name)
      await queryClient.invalidateQueries({ queryKey: ['manage-projects'] })

      if (projectId) {
        await navigate('/admin/content/projects', { replace: true })
      }
    },
  })

  const selected = projects.data?.find((project) => project.id === projectId)

  if (projects.isError) {
    return (
      <ErrorPanel
        message={describeError(projects.error)}
        traceId={traceIdOf(projects.error)}
        onRetry={() => void projects.refetch()}
      />
    )
  }

  return (
    <div className="space-y-8">
      <PageHeader
        title={t('projects.title')}
        actions={
          <Button asChild>
            <Link to="/admin/content/projects/new">
              <Plus /> {t('projects.new')}
            </Link>
          </Button>
        }
      />

      {projects.isPending ? (
        <LoadingState />
      ) : (projects.data?.length ?? 0) === 0 ? (
        <EmptyState title={t('projects.emptyTitle')} description={t('projects.emptyDescription')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('projects.name')}</TableHead>
              <TableHead>{t('projects.slug')}</TableHead>
              <TableHead>{t('common.status')}</TableHead>
              <TableHead className="text-right">{t('common.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {projects.data!.map((project) => (
              <TableRow key={project.id}>
                <TableCell className="font-medium">{project.name}</TableCell>
                <TableCell className="text-muted-foreground font-mono text-xs">/{project.slug}</TableCell>
                <TableCell>
                  <Badge variant={project.isPublished ? 'success' : 'warning'}>
                    {project.isPublished ? t('projects.published') : t('projects.draft')}
                  </Badge>
                </TableCell>
                <TableCell>
                  <div className="flex flex-wrap items-center justify-end gap-2">
                    <Button asChild variant="outline" size="sm">
                      <Link to={`/admin/content/projects/${project.id}`}>{t('common.edit')}</Link>
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={setPublished.isPending}
                      onClick={() => setPublished.mutate({ project, published: !project.isPublished })}
                    >
                      {project.isPublished ? t('projects.unpublish') : t('projects.publish')}
                    </Button>
                    <Button variant="destructive" size="sm" disabled={remove.isPending} onClick={() => setPendingDelete(project)}>
                      {t('common.delete')}
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {/*
        `new` is a sentinel, not a real id: it opens the same form with empty fields, which avoids a route
        that would duplicate the editor just to signal "no project selected".
      */}
      <ProjectEditor
        key={projectId ?? 'none'}
        project={projectId === 'new' ? null : (selected ?? null)}
        creating={projectId === 'new'}
        missing={projectId !== undefined && projectId !== 'new' && projects.data !== undefined && !selected}
      />

      <AlertDialog open={pendingDelete !== null} onOpenChange={(open) => !open && setPendingDelete(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('projects.deleteTitle')}</AlertDialogTitle>
            <AlertDialogDescription>
              {t('projects.deleteDescription', { name: pendingDelete?.name ?? '' })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (pendingDelete) {
                  remove.mutate(pendingDelete)
                }
              }}
            >
              {t('common.delete')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

function ProjectEditor({
  project,
  creating,
  missing,
}: {
  project: ManagedProject | null
  creating: boolean
  missing: boolean
}) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const { t } = useI18n()
  const schema = useMemo(() => makeSchema(t), [t])
  const [form, setForm] = useState<ProjectFields>(() => projectToForm(project))
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<TextField, string>>>({})

  const save = useMutation({
    mutationFn: (values: ProjectFields) => contentAdminApi.saveProject(toProjectRequest(values)),
    meta: { handlesErrors: true },
    onSuccess: async (saved) => {
      notify.success(t('projects.saved'), saved.name)
      await queryClient.invalidateQueries({ queryKey: ['manage-projects'] })
      await navigate('/admin/content/projects', { replace: true })
    },
    onError: (failure) => reportFormError(failure, t('projects.saveFailed')),
  })

  if (missing) {
    return (
      <ErrorPanel title={t('projects.notFoundTitle')} message={t('projects.notFoundMessage')} />
    )
  }

  if (!project && !creating) {
    return null
  }

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse(form)

    if (!parsed.success) {
      setFieldErrors(zodFieldErrors<TextField>(parsed.error))

      return
    }

    setFieldErrors({})
    save.mutate(form)
  }

  const serverErrors = serverFieldErrors(save.error)

  const field = (key: TextField, label: string, extra?: { type?: string; hint?: string; className?: string }) => (
    <FormField
      label={label}
      htmlFor={key}
      error={fieldErrors[key] ?? serverErrors[key]?.[0]}
      hint={extra?.hint}
      className={extra?.className}
    >
      <Input
        type={extra?.type ?? 'text'}
        value={form[key]}
        onChange={(event) => setForm({ ...form, [key]: event.target.value })}
      />
    </FormField>
  )

  return (
    <Section
      title={form.id ? t('projects.editTitle') : t('projects.newTitle')}
      description={form.id ? undefined : t('projects.newDescription')}
    >
      <form onSubmit={submit} noValidate className="grid gap-5">
        <div className="grid items-start gap-5 sm:grid-cols-2">
          {field('name', t('projects.name'))}
          {field('slug', t('projects.slug'), { hint: t('projects.slugHint') })}
          {field('summary', t('projects.summary'), { className: 'sm:col-span-2' })}

          <FormField
            label={t('projects.description')}
            htmlFor="description"
            error={fieldErrors.description ?? serverErrors.description?.[0]}
            className="sm:col-span-2"
          >
            <Textarea rows={8} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
          </FormField>

          {field('url', t('projects.liveUrl'), { type: 'url' })}
          {field('repositoryUrl', t('projects.repositoryUrl'), { type: 'url' })}
          {field('startedOn', t('projects.startedOn'), { type: 'date' })}
          {field('completedOn', t('projects.completedOn'), { type: 'date', hint: t('projects.completedHint') })}
          {field('position', t('projects.position'), { hint: t('projects.positionHint') })}
          {field('technologies', t('projects.technologies'), { hint: t('projects.technologiesHint') })}
        </div>

        <div className="flex items-center gap-2">
          <Checkbox
            id="isPublished"
            checked={form.isPublished}
            onCheckedChange={(checked) => setForm({ ...form, isPublished: checked === true })}
          />
          <Label htmlFor="isPublished" className="font-normal">
            {t('projects.published')}
          </Label>
        </div>

        <div>
          <Button type="submit" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" />}
            {save.isPending ? t('common.saving') : t('projects.submit')}
          </Button>
        </div>
      </form>
    </Section>
  )
}
