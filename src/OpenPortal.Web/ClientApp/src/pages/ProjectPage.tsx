import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { publicContentApi } from '../api/content'
import { Button, ErrorPanel } from '../components/ui'
import { describeError, traceIdOf } from '../hooks/useRetryableError'

/** One project, addressed by its stable slug. */
export function ProjectPage() {
  const { slug } = useParams<{ slug: string }>()

  const project = useQuery({
    queryKey: ['public-project', slug],
    queryFn: ({ signal }) => publicContentApi.project(slug!, signal),
    // Without this a slug of undefined would fire a request to /api/content/projects/ while the route is
    // still resolving its parameters.
    enabled: Boolean(slug),
  })

  if (project.isError) {
    const notFound = project.error !== null && 'status' in project.error && project.error.status === 404

    return (
      <ErrorPanel
        title={notFound ? 'Project not found' : 'This project could not be loaded'}
        message={
          notFound
            ? 'It may have been unpublished, or the link may be out of date.'
            : describeError(project.error)
        }
        traceId={traceIdOf(project.error)}
        onRetry={notFound ? undefined : () => project.refetch()}
      >
        <Link to="/projects">
          <Button variant="outline">Back to all projects</Button>
        </Link>
      </ErrorPanel>
    )
  }

  if (project.isPending) {
    return (
      <p role="status" className="text-muted-foreground py-8 text-center text-sm">
        Loading…
      </p>
    )
  }

  const data = project.data

  return (
    <article className="space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">{data.name}</h1>
        {data.summary && <p className="text-muted-foreground mt-2 text-lg">{data.summary}</p>}
      </div>

      {data.description && (
        <p className="max-w-2xl leading-relaxed whitespace-pre-line">{data.description}</p>
      )}

      {data.technologies.length > 0 && (
        <ul className="flex flex-wrap gap-2 text-xs" aria-label="Technologies">
          {data.technologies.map((technology) => (
            <li key={technology.name} className="bg-muted text-muted-foreground rounded-full px-3 py-1">
              {technology.name}
            </li>
          ))}
        </ul>
      )}

      <dl className="grid gap-4 text-sm sm:grid-cols-2">
        {(data.startedOn || data.completedOn) && (
          <div>
            <dt className="text-muted-foreground">Period</dt>
            <dd className="mt-1">
              {data.startedOn ?? '—'}
              {data.completedOn ? ` – ${data.completedOn}` : ' – ongoing'}
            </dd>
          </div>
        )}
        {data.url && (
          <div>
            <dt className="text-muted-foreground">Live site</dt>
            <dd className="mt-1">
              <a href={data.url} target="_blank" rel="noopener noreferrer" className="underline underline-offset-4">
                {data.url}
              </a>
            </dd>
          </div>
        )}
        {data.repositoryUrl && (
          <div>
            <dt className="text-muted-foreground">Source</dt>
            <dd className="mt-1">
              <a
                href={data.repositoryUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="underline underline-offset-4"
              >
                {data.repositoryUrl}
              </a>
            </dd>
          </div>
        )}
      </dl>
    </article>
  )
}