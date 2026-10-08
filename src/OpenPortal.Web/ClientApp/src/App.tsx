import { MutationCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createBrowserRouter } from 'react-router-dom'
import { ApiError } from '@/api/client'
import type { Crumb, RouteHandle } from '@/components/AppBreadcrumbs'
import { ErrorBoundary } from '@/components/ErrorBoundary'
import { ThemeProvider } from '@/components/theme-provider'
import { I18nProvider, LanguageSync } from '@/i18n/I18nProvider'
import { translate } from '@/i18n/store'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'
import { notify } from '@/hooks/useToast'
import { SESSION_QUERY_KEY } from '@/hooks/useSession'
import { AccessDeniedPage } from '@/pages/AccessDeniedPage'
import { AccountPage } from '@/pages/AccountPage'
import { AdminAccessPage } from '@/pages/AdminAccessPage'
import { AdminApplicationsPage } from '@/pages/AdminApplicationsPage'
import { AdminAuditPage } from '@/pages/AdminAuditPage'
import { AdminSettingsPage } from '@/pages/AdminSettingsPage'
import { AdminGroupsPage } from '@/pages/AdminGroupsPage'
import { AdminPagePermissionsPage } from '@/pages/AdminPagePermissionsPage'
import { AdminUsersPage } from '@/pages/AdminUsersPage'
import { ContentProfilePage } from '@/pages/ContentProfilePage'
import { ContentProjectPage } from '@/pages/ContentProjectPage'
import { DashboardPage } from '@/pages/DashboardPage'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { SignInPage } from '@/pages/SignInPage'
import { AppLayout } from '@/routes/AppLayout'
import { AuthLayout } from '@/routes/AuthLayout'
import { PageRoute, ProtectedRoute, PublicOnlyRoute } from '@/routes/guards'

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
      notify.warning(translate('clientError.sessionEnded'), translate('clientError.signInAgain'))
      void queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY })

      return
    }

    notify.fromError(error, translate('clientError.actionFailed'))
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

const crumbs = (...steps: Crumb[]): RouteHandle => ({ crumbs: steps })
const page = (key: string, ...steps: Crumb[]): RouteHandle => ({ crumbs: steps, page: key })

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
            // Each `handle.crumbs` adds steps to the header breadcrumb trail (see AppBreadcrumbs).
            children: [
              { index: true, element: <DashboardPage />, handle: crumbs({ labelKey: 'nav.dashboard', to: '/' }) },
              { path: 'account', element: <AccountPage />, handle: crumbs({ labelKey: 'nav.account' }) },
              { path: 'access-denied', element: <AccessDeniedPage />, handle: crumbs({ labelKey: 'nav.accessDenied' }) },
              {
                // Each route names its portal page (`page`, a server `PortalPages` key); PageRoute lets in the
                // groups granted that page and administrators. A route without a page is for administrators only.
                element: <PageRoute />,
                handle: crumbs({ labelKey: 'nav.administration' }),
                children: [
                  {
                    handle: crumbs({ labelKey: 'nav.identity' }),
                    children: [
                      { path: 'admin/users', element: <AdminUsersPage />, handle: page('users', { labelKey: 'nav.users' }) },
                      { path: 'admin/groups', element: <AdminGroupsPage />, handle: page('groups', { labelKey: 'nav.groups' }) },
                      { path: 'admin/access', element: <AdminAccessPage />, handle: page('access', { labelKey: 'nav.access' }) },
                      {
                        path: 'admin/page-permissions',
                        element: <AdminPagePermissionsPage />,
                        handle: crumbs({ labelKey: 'nav.pagePermissions' }),
                      },
                    ],
                  },
                  {
                    path: 'admin/applications',
                    element: <AdminApplicationsPage />,
                    handle: page('applications', { labelKey: 'nav.applications' }),
                  },
                  {
                    handle: crumbs({ labelKey: 'nav.security' }),
                    children: [
                      { path: 'admin/audit', element: <AdminAuditPage />, handle: page('audit', { labelKey: 'nav.audit' }) },
                      // No page: portal settings are for administrators only.
                      { path: 'admin/settings', element: <AdminSettingsPage />, handle: crumbs({ labelKey: 'nav.settings' }) },
                    ],
                  },
                  {
                    handle: crumbs({ labelKey: 'nav.content' }),
                    children: [
                      {
                        path: 'admin/content/profile',
                        element: <ContentProfilePage />,
                        handle: page('content.profile', { labelKey: 'nav.profile' }),
                      },
                      {
                        path: 'admin/content/projects',
                        element: <ContentProjectPage />,
                        handle: page('content.projects', { labelKey: 'nav.projects' }),
                      },
                      {
                        path: 'admin/content/projects/:projectId',
                        element: <ContentProjectPage />,
                        handle: page(
                          'content.projects',
                          { labelKey: 'nav.projects', to: '/admin/content/projects' },
                          { labelKey: 'common.edit' },
                        ),
                      },
                    ],
                  },
                ],
              },
              { path: '*', element: <NotFoundPage />, handle: crumbs({ labelKey: 'nav.notFound' }) },
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
      <I18nProvider>
        <QueryClientProvider client={queryClient}>
          <LanguageSync />
          <TooltipProvider>
            <RouterProvider router={router} />
          </TooltipProvider>
          <Toaster />
        </QueryClientProvider>
      </I18nProvider>
    </ThemeProvider>
  )
}
