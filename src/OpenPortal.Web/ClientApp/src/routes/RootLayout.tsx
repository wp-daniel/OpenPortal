import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import { authApi } from '../api/auth'
import { useSession } from '../hooks/useSession'
import { Button } from '../components/ui'

/**
 * The frame every route renders inside: skip link, header, navigation, footer.
 *
 * The skip link is first in the DOM so it is the first thing a keyboard user reaches, which is the only way
 * to jump past the navigation on every page rather than tabbing through it.
 */
export function RootLayout() {
  const { session, user, isAdministrator } = useSession()
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  const signOut = useMutation({
    mutationFn: () => authApi.logout(),
    onSuccess: async () => {
      // Clear every cached read before leaving: a stale user list or content payload would otherwise still be
      // rendered to the next person who signs in on this device.
      queryClient.clear()
      await navigate('/', { replace: true })
    },
  })

  return (
    <div className="min-h-screen">
      <a
        href="#main"
        className="bg-primary text-primary-foreground sr-only focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-50 focus:rounded-md focus:px-4 focus:py-2"
      >
        Skip to content
      </a>

      <header className="border-border bg-background/95 sticky top-0 z-40 border-b backdrop-blur">
        <div className="mx-auto flex max-w-5xl items-center gap-6 px-4 py-3">
          <Link to="/" className="text-base font-semibold tracking-tight">
            OpenPortal
          </Link>

          <nav aria-label="Main" className="flex flex-1 items-center gap-1 text-sm">
            <NavLink
              to="/"
              end
              className={({ isActive }) =>
                `rounded-md px-3 py-1.5 ${isActive ? 'bg-accent text-accent-foreground font-medium' : 'text-muted-foreground hover:text-foreground'}`
              }
            >
              Home
            </NavLink>
            <NavLink
              to="/projects"
              className={({ isActive }) =>
                `rounded-md px-3 py-1.5 ${isActive ? 'bg-accent text-accent-foreground font-medium' : 'text-muted-foreground hover:text-foreground'}`
              }
            >
              Projects
            </NavLink>

            {isAdministrator && (
              <>
                <NavLink
                  to="/admin/content/profile"
                  className={({ isActive }) =>
                    `rounded-md px-3 py-1.5 ${isActive ? 'bg-accent text-accent-foreground font-medium' : 'text-muted-foreground hover:text-foreground'}`
                  }
                >
                  Profile
                </NavLink>
                <NavLink
                  to="/admin/content/projects"
                  className={({ isActive }) =>
                    `rounded-md px-3 py-1.5 ${isActive ? 'bg-accent text-accent-foreground font-medium' : 'text-muted-foreground hover:text-foreground'}`
                  }
                >
                  Manage projects
                </NavLink>
                <NavLink
                  to="/admin/users"
                  className={({ isActive }) =>
                    `rounded-md px-3 py-1.5 ${isActive ? 'bg-accent text-accent-foreground font-medium' : 'text-muted-foreground hover:text-foreground'}`
                  }
                >
                  Users
                </NavLink>
              </>
            )}
          </nav>

          {session.isAuthenticated ? (
            <div className="flex items-center gap-2 text-sm">
              <Link to="/account" className="text-muted-foreground hover:text-foreground">
                {user?.displayName ?? user?.email}
              </Link>
              <Button
                variant="ghost"
                onClick={() => signOut.mutate()}
                disabled={signOut.isPending}
              >
                {signOut.isPending ? 'Signing out…' : 'Sign out'}
              </Button>
            </div>
          ) : (
            <Button variant="outline" onClick={() => navigate('/sign-in')}>
              Sign in
            </Button>
          )}
        </div>
      </header>

      <main id="main" className="mx-auto max-w-5xl px-4 py-10">
        <Outlet />
      </main>

      <footer className="border-border mt-16 border-t">
        <div className="text-muted-foreground mx-auto max-w-5xl px-4 py-6 text-xs">
          OpenPortal
        </div>
      </footer>
    </div>
  )
}

