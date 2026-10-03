import { MutationCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createBrowserRouter } from 'react-router-dom'
import { ApiError } from '@/api/client'
import { ErrorBoundary } from '@/components/ErrorBoundary'
import { ThemeProvider } from '@/components/theme-provider'
import { Toaster } from '@/components/ui/sonner'
import { notify } from '@/hooks/useToast'
import { SESSION_QUERY_KEY } from '@/hooks/useSession'
import { AccountPage } from '@/pages/AccountPage'
import { AdminUsersPage } from '@/pages/AdminUsersPage'
import { ContentProfilePage } from '@/pages/ContentProfilePage'
import { ContentProjectPage } from '@/pages/ContentProjectPage'
import { DashboardPage } from '@/pages/DashboardPage'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { SignInPage } from '@/pages/SignInPage'
import { AppLayout } from '@/routes/AppLayout'
import { AuthLayout } from '@/routes/AuthLayout'
import { AdminRoute, ProtectedRoute, PublicOnlyRoute } from '@/routes/guards'

/**
 * Mutations a form handles itself set `meta: { handlesErrors: true }`. Every other failed mutation gets a
 * toast here, so a failure can never be silent just because one call site forgot an `onError`.
 * A 401 means the session ended; refreshing it lets the route guards send the user to /sign-in.
 */
const mutationCache = new MutationCache({
  onError: (error, _variables, _context, mutation) => {
    if (mutation.meta?.handlesErrors) {
      return
    }

    if (error instanceof ApiError && error.isUnauthenticated) {
      notify.warning('Your session has ended', 'Sign in again to continue.')
      void queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })

      return
    }

    notify.fromError(error, 'The action could not be completed')
  },
})

/**
 * Shared client defaults.
 *
 * The single retry that matters is for GET requests: a failed read can be retried safely, while retrying a
 * write risks duplicating a side effect the server already committed. Anything the server rejected is not
 * retried at all, because repeating a request it has already judged invalid only delays the same answer.
 */
const queryClient = new QueryClient({
  mutationCache,
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: (failureCount, error) => {
        if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
          return false
        }

        return failureCount < 2
      },
      refetchOnWindowFocus: false,
    },
    mutations: {
      retry: false,
    },
  },
})

const router = createBrowserRouter([
  {
    errorElement: <ErrorBoundary />,
    children: [
      {
        element: <PublicOnlyRoute />,
        children: [{ element: <AuthLayout />, children: [{ path: 'sign-in', element: <SignInPage /> }] }],
      },
      {
        element: <ProtectedRoute />,
        children: [
          {
            element: <AppLayout />,
            children: [
              { index: true, element: <DashboardPage /> },
              { path: 'account', element: <AccountPage /> },
              {
                element: <AdminRoute />,
                children: [
                  { path: 'admin/users', element: <AdminUsersPage /> },
                  { path: 'admin/content/profile', element: <ContentProfilePage /> },
                  { path: 'admin/content/projects', element: <ContentProjectPage /> },
                  { path: 'admin/content/projects/:projectId', element: <ContentProjectPage /> },
                ],
              },
              { path: '*', element: <NotFoundPage /> },
            ],
          },
        ],
      },
    ],
  },
])

export function App() {
  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
        <Toaster />
      </QueryClientProvider>
    </ThemeProvider>
  )
}
