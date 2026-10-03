import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Loader2, Plus } from 'lucide-react'
import { useState, type FormEvent } from 'react'
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
import { describeError, traceIdOf } from '@/lib/errors'
import { notify } from '@/hooks/useToast'
import { reportFormError, serverFieldErrors, zodFieldErrors } from '@/lib/forms'

const schema = z
  .object({
    name: z.string().trim().min(1, 'A name is required.').max(160, 'Keep the name under 160 characters.'),
    // Only shape is checked here. The server owns slug normalisation and reports an invalid slug itself, so a
    // second slug regex in the browser could only disagree with the authoritative one.
    slug: z
      .string()
      .trim()
      .min(1, 'A slug is required.')
      .max(160, 'Keep the slug under 160 characters.')
      .regex(/^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$/, 'Use lowercase letters, numbers and single hyphens.'),
    summary: z.string().max(400, 'Keep the summary under 400 characters.'),
    description: z.string().max(8_000, 'Keep the description under 8000 characters.'),
    url: z.union([z.literal(''), z.string().url('Enter an absolute URL.')]),
    repositoryUrl: z.union([z.literal(''), z.string().url('Enter an absolute URL.')]),
    position: z.union([z.literal(''), z.string().regex(/^\d+$/, 'Position must be a whole number.')]),
    startedOn: z.union([z.literal(''), z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Use the date picker.')]),
    completedOn: z.union([z.literal(''), z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Use the date picker.')]),
  })
  .refine((values) => !values.startedOn || !values.completedOn || values.completedOn >= values.startedOn, {
    message: 'Completion cannot precede the start.',
    path: ['completedOn'],
  })

type TextField = Exclude<keyof ProjectFields, 'id' | 'isPublished'>

/** The project editor: lists every project and creates or edits the one named in the route. */
export function ContentProjectPage() {
  const { projectId } = useParams<{ projectId: string }>()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
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
      notify.success(published ? 'Project published' : 'Project unpublished', project.name)
      await queryClient.invalidateQueries({ queryKey: ['manage-projects'] })
    },
  })

  const remove = useMutation({
    mutationFn: (project: ManagedProject) => contentAdminApi.deleteProject(project.id),
    onSuccess: async (_result, project) => {
      notify.success('Project deleted', project.name)
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
        title="Projects"
        actions={
          <Button asChild>
            <Link to="/admin/content/projects/new">
              <Plus /> New project
            </Link>
          </Button>
        }
      />

      {projects.isPending ? (
        <LoadingState />
      ) : (projects.data?.length ?? 0) === 0 ? (
        <EmptyState title="No projects yet" description="Create the first one to get started." />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Slug</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {projects.data!.map((project) => (
              <TableRow key={project.id}>
                <TableCell className="font-medium">{project.name}</TableCell>
                <TableCell className="text-muted-foreground font-mono text-xs">/{project.slug}</TableCell>
                <TableCell>
                  <Badge variant={project.isPublished ? 'success' : 'warning'}>
                    {project.isPublished ? 'Published' : 'Draft'}
                  </Badge>
                </TableCell>
                <TableCell>
                  <div className="flex flex-wrap items-center justify-end gap-2">
                    <Button asChild variant="outline" size="sm">
                      <Link to={`/admin/content/projects/${project.id}`}>Edit</Link>
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={setPublished.isPending}
                      onClick={() => setPublished.mutate({ project, published: !project.isPublished })}
                    >
                      {project.isPublished ? 'Unpublish' : 'Publish'}
                    </Button>
                    <Button variant="destructive" size="sm" disabled={remove.isPending} onClick={() => setPendingDelete(project)}>
                      Delete
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
            <AlertDialogTitle>Delete this project?</AlertDialogTitle>
            <AlertDialogDescription>
              {pendingDelete?.name} will be permanently removed. This cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (pendingDelete) {
                  remove.mutate(pendingDelete)
                }
              }}
            >
              Delete
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
  const [form, setForm] = useState<ProjectFields>(() => projectToForm(project))
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<TextField, string>>>({})

  const save = useMutation({
    mutationFn: (values: ProjectFields) => contentAdminApi.saveProject(toProjectRequest(values)),
    meta: { handlesErrors: true },
    onSuccess: async (saved) => {
      notify.success('Project saved', saved.name)
      await queryClient.invalidateQueries({ queryKey: ['manage-projects'] })
      await navigate('/admin/content/projects', { replace: true })
    },
    onError: (failure) => reportFormError(failure, 'The project could not be saved'),
  })

  if (missing) {
    return (
      <ErrorPanel title="Project not found" message="It may have been deleted. Pick another project from the list." />
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
      title={form.id ? 'Edit project' : 'New project'}
      description={form.id ? undefined : 'The slug becomes the project’s address.'}
    >
      <form onSubmit={submit} noValidate className="grid gap-5">
        <div className="grid items-start gap-5 sm:grid-cols-2">
          {field('name', 'Name')}
          {field('slug', 'Slug', { hint: 'Lowercase, hyphen-separated.' })}
          {field('summary', 'Summary', { className: 'sm:col-span-2' })}

          <FormField
            label="Description"
            htmlFor="description"
            error={fieldErrors.description ?? serverErrors.description?.[0]}
            className="sm:col-span-2"
          >
            <Textarea rows={8} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
          </FormField>

          {field('url', 'Live URL', { type: 'url' })}
          {field('repositoryUrl', 'Repository URL', { type: 'url' })}
          {field('startedOn', 'Started on', { type: 'date' })}
          {field('completedOn', 'Completed on', { type: 'date', hint: 'Leave empty while ongoing.' })}
          {field('position', 'Display position', { hint: 'Lower sorts first. Empty sorts last.' })}
          {field('technologies', 'Technologies', { hint: 'Comma-separated; shared between projects.' })}
        </div>

        <div className="flex items-center gap-2">
          <Checkbox
            id="isPublished"
            checked={form.isPublished}
            onCheckedChange={(checked) => setForm({ ...form, isPublished: checked === true })}
          />
          <Label htmlFor="isPublished" className="font-normal">
            Published
          </Label>
        </div>

        <div>
          <Button type="submit" disabled={save.isPending}>
            {save.isPending && <Loader2 className="animate-spin" />}
            {save.isPending ? 'Saving…' : 'Save project'}
          </Button>
        </div>
      </form>
    </Section>
  )
}
