import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  AppWindow,
  ChevronRight,
  ChevronsUpDown,
  FileText,
  History,
  LayoutDashboard,
  LogOut,
  ShieldCheck,
  UserRound,
  type LucideIcon,
} from 'lucide-react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { authApi } from '@/api/auth'
import { BrandMark } from '@/components/BrandMark'
import { UserAvatar } from '@/components/UserAvatar'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
  SidebarRail,
  useSidebar,
} from '@/components/ui/sidebar'
import { useSession } from '@/hooks/useSession'
import { useI18n } from '@/i18n/useI18n'

interface NavLeaf {
  readonly to: string
  /** Translation key of the link text. */
  readonly labelKey: string
  /** Match only this exact path (the dashboard at `/` would otherwise be active everywhere). */
  readonly end?: boolean
  /**
   * Administration links only: the portal page (server `PortalPages` key) the link opens, shown to holders of
   * the page. Without one the link is for administrators only. Keep it equal to the route's `handle.page`.
   */
  readonly page?: string
}

interface NavEntry extends NavLeaf {
  readonly icon: LucideIcon
}

interface NavBranch {
  /** Translation key of the submenu title. */
  readonly labelKey: string
  readonly icon: LucideIcon
  readonly children: readonly NavLeaf[]
}

const portalLinks: readonly NavEntry[] = [
  { to: '/', labelKey: 'nav.dashboard', icon: LayoutDashboard, end: true },
  { to: '/account', labelKey: 'nav.account', icon: UserRound },
]

/** Administration, grouped by area so the sidebar stays short as the portal grows. */
const adminBranches: readonly NavBranch[] = [
  {
    labelKey: 'nav.identity',
    icon: ShieldCheck,
    children: [
      { to: '/admin/users', labelKey: 'nav.users', page: 'users' },
      { to: '/admin/groups', labelKey: 'nav.groups', page: 'groups' },
      { to: '/admin/access', labelKey: 'nav.access', page: 'access' },
      { to: '/admin/page-permissions', labelKey: 'nav.pagePermissions' },
    ],
  },
  {
    labelKey: 'nav.applications',
    icon: AppWindow,
    children: [{ to: '/admin/applications', labelKey: 'nav.applications', page: 'applications' }],
  },
  {
    labelKey: 'nav.security',
    icon: History,
    children: [{ to: '/admin/audit', labelKey: 'nav.audit', page: 'audit' }],
  },
  {
    labelKey: 'nav.content',
    icon: FileText,
    children: [
      { to: '/admin/content/profile', labelKey: 'nav.profile', page: 'content.profile' },
      { to: '/admin/content/projects', labelKey: 'nav.projects', page: 'content.projects' },
    ],
  },
]

/** The administration branches with only the links the user may open; branches left empty disappear. */
function useAdminBranches(): readonly NavBranch[] {
  const { isAdministrator, canOpen } = useSession()

  return adminBranches
    .map((branch) => ({
      ...branch,
      children: branch.children.filter((child) => (child.page === undefined ? isAdministrator : canOpen(child.page))),
    }))
    .filter((branch) => branch.children.length > 0)
}

function useIsActive() {
  const { pathname } = useLocation()

  return ({ to, end }: NavLeaf) => (end ? pathname === to : pathname === to || pathname.startsWith(`${to}/`))
}

/**
 * The primary navigation: brand, the portal's own pages, the administration pages the user may open and
 * the signed-in user's menu at the bottom.
 *
 * It collapses to an icon rail on desktop (Ctrl/Cmd+B or the header button; the choice is remembered in the
 * `sidebar_state` cookie) and becomes a sheet on mobile. While collapsed, a submenu cannot unfold inside the
 * rail, so its icon opens the same links as a dropdown beside it.
 */
export function AppSidebar() {
  const { t } = useI18n()
  const branches = useAdminBranches()
  const isActive = useIsActive()
  const { isMobile, setOpenMobile } = useSidebar()

  // On mobile the sidebar is a sheet over the page; following a link should reveal the page.
  const close = () => {
    if (isMobile) {
      setOpenMobile(false)
    }
  }

  return (
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" asChild>
              <Link to="/" onClick={close}>
                <div className="bg-sidebar-primary/5 flex aspect-square size-8 items-center justify-center rounded-lg border">
                  <BrandMark className="size-5" />
                </div>
                <div className="grid flex-1 text-left leading-tight">
                  <span className="truncate font-semibold tracking-tight">OpenPortal</span>
                  <span className="text-muted-foreground truncate text-xs">{t('nav.tagline')}</span>
                </div>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupLabel>{t('nav.portal')}</SidebarGroupLabel>
          <SidebarMenu>
            {portalLinks.map((link) => (
              <SidebarMenuItem key={link.to}>
                <SidebarMenuButton asChild isActive={isActive(link)} tooltip={t(link.labelKey)}>
                  <Link to={link.to} onClick={close}>
                    <link.icon />
                    <span>{t(link.labelKey)}</span>
                  </Link>
                </SidebarMenuButton>
              </SidebarMenuItem>
            ))}
          </SidebarMenu>
        </SidebarGroup>

        {branches.length > 0 && (
          <SidebarGroup>
            <SidebarGroupLabel>{t('nav.administration')}</SidebarGroupLabel>
            <SidebarMenu>
              {branches.map((branch) => (
                <NavBranchItem key={branch.labelKey} branch={branch} onNavigate={close} />
              ))}
            </SidebarMenu>
          </SidebarGroup>
        )}
      </SidebarContent>

      <SidebarFooter>
        <NavUser onNavigate={close} />
      </SidebarFooter>

      <SidebarRail />
    </Sidebar>
  )
}

/** A submenu: unfolds in place when the sidebar is expanded, opens as a dropdown when it is an icon rail. */
function NavBranchItem({ branch, onNavigate }: { branch: NavBranch; onNavigate: () => void }) {
  const { t } = useI18n()
  const isActive = useIsActive()
  const { state, isMobile } = useSidebar()
  const containsActive = branch.children.some(isActive)

  // A branch with a single page is just a link; a submenu of one would be an extra click for nothing.
  if (branch.children.length === 1) {
    const [only] = branch.children

    return (
      <SidebarMenuItem>
        <SidebarMenuButton asChild isActive={containsActive} tooltip={t(branch.labelKey)}>
          <Link to={only.to} onClick={onNavigate}>
            <branch.icon />
            <span>{t(branch.labelKey)}</span>
          </Link>
        </SidebarMenuButton>
      </SidebarMenuItem>
    )
  }

  if (state === 'collapsed' && !isMobile) {
    return (
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <SidebarMenuButton isActive={containsActive} tooltip={t(branch.labelKey)}>
              <branch.icon />
              <span>{t(branch.labelKey)}</span>
            </SidebarMenuButton>
          </DropdownMenuTrigger>
          <DropdownMenuContent side="right" align="start" className="min-w-48">
            <DropdownMenuLabel className="text-muted-foreground text-xs font-normal">
              {t(branch.labelKey)}
            </DropdownMenuLabel>
            {branch.children.map((child) => (
              <DropdownMenuItem key={child.to} asChild className={isActive(child) ? 'font-medium' : undefined}>
                <Link to={child.to}>{t(child.labelKey)}</Link>
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    )
  }

  return (
    <Collapsible asChild defaultOpen={containsActive} className="group/collapsible">
      <SidebarMenuItem>
        <CollapsibleTrigger asChild>
          <SidebarMenuButton isActive={containsActive} tooltip={t(branch.labelKey)}>
            <branch.icon />
            <span>{t(branch.labelKey)}</span>
            <ChevronRight className="ml-auto transition-transform duration-200 group-data-[state=open]/collapsible:rotate-90" />
          </SidebarMenuButton>
        </CollapsibleTrigger>
        <CollapsibleContent>
          <SidebarMenuSub>
            {branch.children.map((child) => (
              <SidebarMenuSubItem key={child.to}>
                <SidebarMenuSubButton asChild isActive={isActive(child)}>
                  <Link to={child.to} onClick={onNavigate}>
                    <span>{t(child.labelKey)}</span>
                  </Link>
                </SidebarMenuSubButton>
              </SidebarMenuSubItem>
            ))}
          </SidebarMenuSub>
        </CollapsibleContent>
      </SidebarMenuItem>
    </Collapsible>
  )
}

/** The signed-in user at the foot of the sidebar: picture, name and email, opening Account and Sign out. */
function NavUser({ onNavigate }: { onNavigate: () => void }) {
  const { user } = useSession()
  const { t } = useI18n()
  const { isMobile } = useSidebar()
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  const signOut = useMutation({
    mutationFn: () => authApi.logout(),
    onSuccess: async () => {
      // Clear every cached read before leaving: stale data would otherwise be rendered to the next person
      // who signs in on this device.
      queryClient.clear()
      await navigate('/sign-in', { replace: true })
    },
  })

  if (!user) {
    return null
  }

  const identity = (
    <>
      <UserAvatar user={user} className="rounded-lg" />
      <div className="grid flex-1 text-left text-sm leading-tight">
        <span className="truncate font-medium">{user.displayName}</span>
        <span className="text-muted-foreground truncate text-xs">{user.email}</span>
      </div>
    </>
  )

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <SidebarMenuButton
              size="lg"
              className="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
              aria-label={t('nav.userMenu')}
            >
              {identity}
              <ChevronsUpDown className="ml-auto size-4" />
            </SidebarMenuButton>
          </DropdownMenuTrigger>
          <DropdownMenuContent
            className="w-(--radix-dropdown-menu-trigger-width) min-w-56 rounded-lg"
            side={isMobile ? 'bottom' : 'right'}
            align="end"
            sideOffset={4}
          >
            <DropdownMenuLabel className="p-0 font-normal">
              <div className="flex items-center gap-2 px-1 py-1.5">{identity}</div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuItem asChild>
                <Link to="/account" onClick={onNavigate}>
                  <UserRound />
                  {t('nav.account')}
                </Link>
              </DropdownMenuItem>
            </DropdownMenuGroup>
            <DropdownMenuSeparator />
            <DropdownMenuItem disabled={signOut.isPending} onSelect={() => signOut.mutate()}>
              <LogOut />
              {signOut.isPending ? t('nav.signingOut') : t('nav.signOut')}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  )
}
