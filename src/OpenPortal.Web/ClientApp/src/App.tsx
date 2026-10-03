import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createBrowserRouter } from 'react-router-dom'
import { ApiError } from './api/client'
import { ErrorBoundary } from './components/ErrorBoundary'
import { NotFoundPage } from './pages/NotFoundPage'
import { SignInPage } from './pages/SignInPage'
import { AccountPage } from './pages/AccountPage'
import { AdminUsersPage } from './pages/AdminUsersPage'
import { HomePage } from './pages/HomePage'
import { ProjectPage } from './pages/ProjectPage'
import { ContentListPage } from './pages/ContentListPage'
import { ContentProfilePage } from './pages/ContentProfilePage'
import { ContentProjectPage } from './pages/ContentProjectPage'
import { RootLayout } from './routes/RootLayout'

/**
 * Shared client defaults.
 *
 * The single retry that matters is for GET requests: a failed read can be retried safely, while retrying a
 * write risks duplicating a side effect the server already committed. Anything the server rejected is not
 * retried at all, because repeating a request it has already judged invalid only delays the same answer.
 */
const queryClient = new QueryClient({
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
    element: <RootLayout />,
    errorElement: <ErrorBoundary />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'projects', element: <ContentListPage /> },
      { path: 'projects/:slug', element: <ProjectPage /> },
      { path: 'sign-in', element: <SignInPage /> },
      { path: 'account', element: <AccountPage /> },
      { path: 'admin/users', element: <AdminUsersPage /> },
      { path: 'admin/content/profile', element: <ContentProfilePage /> },
      { path: 'admin/content/projects', element: <ContentProjectPage /> },
      { path: 'admin/content/projects/:projectId', element: <ContentProjectPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
])

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}