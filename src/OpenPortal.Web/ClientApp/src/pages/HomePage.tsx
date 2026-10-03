import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { publicContentApi } from '../api/content'
import type { Project } from '../api/types'
import { Card, EmptyState, ErrorPanel } from '../components/ui'
import { describeError, traceIdOf } from '../hooks/useRetryableError'

/** The public landing page: profile plus published projects, in one payload. */
export function HomePage() {
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

  const profile = content.data?.profile
  const projects = content.data?.projects ?? []

  return (
    <div className="space-y-12">
      <section>
        <h1 className="text-3xl font-semibold tracking-tight sm:text-4xl">
          {profile?.displayName ?? 'OpenPortal'}
        </h1>
        {profile?.headline && (
          <p className="text-muted-foreground mt-3 max-w-2xl text-lg">{profile.headline}</p>
        )}
        {profile?.summary && (
          <p className="text-muted-foreground mt-4 max-w-2xl leading-relaxed">{profile.summary}</p>
        )}

        {(profile?.location || profile?.email || (profile?.socialLinks.length ?? 0) > 0) && (
          <div className="text-muted-foreground mt-6 flex flex-wrap items-center gap-4 text-sm">
            {profile?.location && <span>{profile.location}</span>}
            {profile?.email && (
              <a href={`mailto:${profile.email}`} className="hover:text-foreground underline underline-offset-4">
                {profile.email}
              </a>
            )}
            {profile?.socialLinks.map((link) => (
              <a
                key={`${link.platform}-${link.url}`}
                href={link.url}
                target="_blank"
                // noopener stops the opened page from reaching back through window.opener.
                rel="noopener noreferrer"
                className="hover:text-foreground underline underline-offset-4"
              >
                {link.label ?? link.platform}
              </a>
            ))}
          </div>
        )}
      </section>

      <section aria-labelledby="featured-heading" className="space-y-4">
        <div className="flex items-baseline justify-between">
          <h2 id="featured-heading" className="text-xl font-semibold tracking-tight">
            Projects
          </h2>
          {projects.length > 0 && (
            <Link to="/projects" className="text-muted-foreground text-sm hover:text-foreground">
              View all
            </Link>
          )}
        </div>

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
          <div className="grid gap-4 sm:grid-cols-2">
            {projects.slice(0, 4).map((project) => (
              <ProjectCard key={project.slug} project={project} />
            ))}
          </div>
        )}
      </section>
    </div>
  )
}

function ProjectCard({ project }: { project: Project }) {
  return (
    <Card>
      <h3 className="text-base font-semibold">
        <Link
          to={`/projects/${project.slug}`}
          className="after:absolute after:inset-0 hover:underline underline-offset-4"
        >
          {project.name}
        </Link>
      </h3>
      {project.summary && <p className="text-muted-foreground mt-2 text-sm">{project.summary}</p>}
      {project.technologies.length > 0 && (
        <p className="text-muted-foreground mt-3 text-xs">
          {project.technologies.map((technology) => technology.name).join(' · ')}
        </p>
      )}
    </Card>
  )
}