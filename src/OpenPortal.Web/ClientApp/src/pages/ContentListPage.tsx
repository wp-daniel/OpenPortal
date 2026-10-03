import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { publicContentApi } from '../api/content'
import { Card, EmptyState, ErrorPanel } from '../components/ui'
import { describeError, traceIdOf } from '../hooks/useRetryableError'

/** Every published project, listed. */
export function ContentListPage() {
  const content = useQuery({
    queryKey: ['public-content'],
    queryFn: ({ signal }) => publicContentApi.all(signal),
  })

  if (content.isError) {
    return (
      <ErrorPanel
        message={describeError(content.error)}
        traceId={traceIdOf(content.error)}
        onRetry={() => content.refetch()}
      />
    )
  }

  const projects = content.data?.projects ?? []

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold tracking-tight">Projects</h1>

      {content.isPending ? (
        <p role="status" className="text-muted-foreground py-8 text-center text-sm">
          Loading…
        </p>
      ) : projects.length === 0 ? (
        <EmptyState
          title="Nothing published yet"
          description="Projects appear here once an administrator publishes them."
        />
      ) : (
        <ul className="space-y-4">
          {projects.map((project) => (
            <li key={project.slug}>
              <Card>
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h2 className="text-base font-semibold">
                      <Link
                        to={`/projects/${project.slug}`}
                        className="after:absolute after:inset-0 hover:underline underline-offset-4"
                      >
                        {project.name}
                      </Link>
                    </h2>
                    {project.summary && (
                      <p className="text-muted-foreground mt-1 text-sm">{project.summary}</p>
                    )}
                  </div>
                  {project.startedOn && (
                    <span className="text-muted-foreground text-xs">
                      {project.startedOn}
                      {project.completedOn ? ` – ${project.completedOn}` : ' – ongoing'}
                    </span>
                  )}
                </div>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}