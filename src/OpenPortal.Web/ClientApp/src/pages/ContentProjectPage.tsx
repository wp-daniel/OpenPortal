import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { ApiError } from '../api/client'
import {
  contentAdminApi,
  projectToForm,
  toProjectRequest,
  type ProjectForm as ProjectFields,
} from '../api/content'
import type { ManagedProject } from '../api/types'
import { Badge, Button, Card, EmptyState, ErrorPanel, Field } from '../components/ui'
import { describeError, traceIdOf } from '../hooks/useRetryableError'

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
  .refine(
    (values) => !values.startedOn || !values.completedOn || values.completedOn >= values.startedOn,
    { message: 'Completion cannot precede the start.', path: ['completedOn'] },
  )

/** The project editor: lists every project and creates or edits the one named in the route. */
export function ContentProjectPage() {
  const { projectId } = useParams<{ projectId: string }>()
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  const projects = useQuery({
    queryKey: ['manage-projects'],
    queryFn: ({ signal }) => contentAdminApi.listProjects(signal),
  })

  const setPublished = useMutation({
    mutationFn: ({ id, published }: { id: string; published: boolean }) =>
      contentAdminApi.setPublished(id, published),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['manage-projects'] }),
  })

  const remove = useMutation({
    mutationFn: (id: string) => contentAdminApi.deleteProject(id),
    onSuccess: async () => {
      // A deleted project also disappears from the public pages, so both caches are stale.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['manage-projects'] }),
        queryClient.invalidateQueries({ queryKey: ['public-content'] }),
        queryClient.invalidateQueries({ queryKey: ['public-project'] }),
      ])

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
        onRetry={() => projects.refetch()}
      />
    )
  }

  return (
    <div className="space-y-10">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">Projects</h1>
        <Button onClick={() => navigate('/admin/content/projects/new')}>New project</Button>
      </div>

      {projects.isPending ? (
        <p role="status" className="text-muted-foreground py-8 text-center text-sm">
          Loading…
        </p>
      ) : (projects.data?.length ?? 0) === 0 ? (
        <EmptyState title="No projects yet" description="Create the first one to get started." />
      ) : (
        <ul className="space-y-3">
          {projects.data!.map((project) => (
            <li key={project.id}>
              <Card>
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <div className="flex items-center gap-2">
                      <h2 className="text-base font-semibold">{project.name}</h2>
                      <Badge tone={project.isPublished ? 'success' : 'warning'}>
                        {project.isPublished ? 'Published' : 'Draft'}
                      </Badge>
                    </div>
                    <p className="text-muted-foreground mt-1 font-mono text-xs">/{project.slug}</p>
                  </div>

                  <div className="flex items-center gap-2">
                    <Link to={`/admin/content/projects/${project.id}`}>
                      <Button variant="outline">Edit</Button>
                    </Link>
                    <Button
                      variant="outline"
                      disabled={setPublished.isPending}
                      onClick={() =>
                        setPublished.mutate({ id: project.id, published: !project.isPublished })
                      }
                    >
                      {project.isPublished ? 'Unpublish' : 'Publish'}
                    </Button>
                    <Button
                      variant="destructive"
                      disabled={remove.isPending}
                      onClick={() => remove.mutate(project.id)}
                    >
                      Delete
                    </Button>
                  </div>
                </div>
              </Card>
            </li>
          ))}
        </ul>
      )}

      {/*
        `new` is a sentinel, not a real id: it opens the same form with empty fields, which avoids a route
        that would duplicate the editor just to signal "no project selected".
      */}
      <ProjectEditor
        key={projectId ?? 'none'}
        project={projectId === 'new' ? null : (selected ?? null)}
        missing={projectId !== undefined && projectId !== 'new' && projects.data !== undefined && !selected}
      />
    </div>
  )
}

function ProjectEditor({
  project,
  missing,
}: {
  project: ManagedProject | null
  missing: boolean
}) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [form, setForm] = useState<ProjectFields>(() => projectToForm(project))
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})

  const save = useMutation({
    mutationFn: (values: ProjectFields) => contentAdminApi.saveProject(toProjectRequest(values)),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['manage-projects'] }),
        queryClient.invalidateQueries({ queryKey: ['public-content'] }),
      ])

      await navigate('/admin/content/projects', { replace: true })
    },
  })

  if (missing) {
    return (
      <ErrorPanel
        title="Project not found"
        message="It may have been deleted. Pick another project from the list."
      />
    )
  }

  if (!project) {
    return null
  }

  function submit(event: FormEvent) {
    event.preventDefault()

    const parsed = schema.safeParse(form)

    if (!parsed.success) {
      setFieldErrors(
        Object.fromEntries(parsed.error.issues.map((issue) => [issue.path[0] as string, issue.message])),
      )

      return
    }

    setFieldErrors({})
    save.mutate(form)
  }

  const serverErrors = save.error instanceof ApiError ? save.error.fieldErrors : {}
  const summaryError =
    save.isError && !(save.error instanceof ApiError && save.error.status === 400)
      ? describeError(save.error)
      : undefined

  const field = (key: keyof ProjectFields, id: string, label: string, extra?: { type?: string; hint?: string }) => (
    <Field label={label} htmlFor={id} error={fieldErrors[key]} hint={extra?.hint}>
      <input
        id={id}
        type={extra?.type ?? 'text'}
        value={String(form[key])}
        onChange={(event) => setForm({ ...form, [key]: event.target.value })}
        aria-invalid={Boolean(fieldErrors[key])}
        className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
      />
    </Field>
  )

  return (
    <Card
      title={form.id ? 'Edit project' : 'New project'}
      description={
        form.id ? 'Publishing changes are visible on the public site immediately.' : 'The slug becomes the URL.'
      }
    >
      <form onSubmit={submit} noValidate className="space-y-5">
        <div className="grid gap-5 sm:grid-cols-2">
          {field('name', 'name', 'Name')}
          {field('slug', 'slug', 'Slug', { hint: 'Lowercase, hyphen-separated.' })}
        </div>

        {field('summary', 'summary', 'Summary')}

        <Field label="Description" htmlFor="description" error={fieldErrors.description}>
          <textarea
            id="description"
            rows={8}
            value={form.description}
            onChange={(event) => setForm({ ...form, description: event.target.value })}
            className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
          />
        </Field>

        <div className="grid gap-5 sm:grid-cols-2">
          {field('url', 'url', 'Live URL', { type: 'url' })}
          {field('repositoryUrl', 'repositoryUrl', 'Repository URL', { type: 'url' })}
          {field('startedOn', 'startedOn', 'Started on', { type: 'date' })}
          {field('completedOn', 'completedOn', 'Completed on', {
            type: 'date',
            hint: 'Leave empty while ongoing.',
          })}
          {field('position', 'position', 'Display position', {
            hint: 'Lower sorts first. Empty sorts after positioned projects.',
          })}
        </div>

        <Field
          label="Technologies"
          htmlFor="technologies"
          hint="Comma-separated. They are created on first use and shared between projects."
        >
          <input
            id="technologies"
            value={form.technologies}
            onChange={(event) => setForm({ ...form, technologies: event.target.value })}
            className="border-input bg-background w-full rounded-md border px-3 py-2 text-sm"
          />
        </Field>

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={form.isPublished}
            onChange={(event) => setForm({ ...form, isPublished: event.target.checked })}
          />
          Published
        </label>

        <div className="flex items-center gap-3">
          <Button type="submit" disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save project'}
          </Button>
          {save.isSuccess && (
            <span role="status" className="text-muted-foreground text-sm">
              Saved.
            </span>
          )}
          {summaryError && (
            <span role="alert" className="text-destructive text-sm">
              {summaryError}
            </span>
          )}
        </div>

        {serverErrors.slug && (
          <p role="alert" className="text-destructive text-sm">
            {serverErrors.slug[0]}
          </p>
        )}
      </form>
    </Card>
  )
}