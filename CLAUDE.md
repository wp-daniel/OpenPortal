# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

OpenPortal is a reusable application shell (user management, sessions, role-gated admin area, and an OpenID Connect provider that other apps sign in through, with per-app access for users and groups) meant as the base for any ASP.NET Core app such as a CRM: ASP.NET Core 10 API + React 19 client (Vite, Tailwind 4, shadcn/ui), ASP.NET Core Identity, SQLite by default. `README.md` and `ARCHITECTURE.md` are accurate and detailed (they still describe the original portfolio framing); read `ARCHITECTURE.md` before structural changes.

## Commands

Requires .NET SDK 10.0.400 (pinned in `global.json`) and Node 22+. `dotnet tool restore` installs `dotnet-ef` locally.

```bash
dotnet run --project src/OpenPortal.Web                      # builds client, applies migrations, serves (Vite proxied via SpaProxy)
dotnet run --project src/OpenPortal.Web -p:SkipClientBuild=true   # backend only
dotnet test                                                  # all suites
dotnet test tests/OpenPortal.Tests.Unit
dotnet test tests/OpenPortal.Tests.Integration --filter "FullyQualifiedName~ApiTests"   # single class/test
cd src/OpenPortal.Web/ClientApp && npm run dev               # client alone
cd src/OpenPortal.Web/ClientApp && npm run build             # tsc -b + vite build (the client type-check)
cd src/OpenPortal.Web/ClientApp && npm run lint              # oxlint
```

There are no client tests. Credentials (e.g. `BootstrapAdmin:*` to create the first admin) go in `dotnet user-secrets`, never in committed `appsettings*.json`. Running `npm run build` while `dotnet run` is compiling can fail with a missing hashed asset in `wwwroot/assets`; just rebuild.

Add shadcn components with `npx shadcn@latest add <name>` from `ClientApp`. In this setup the CLI may write into a literal `@/` folder: move the files into `src/components/ui` and make sure they import `cn` from `@/lib/utils` (not a `cn` package).

## Architecture

Modular monolith. `src/Modules/{Identity,Content,Access}` each have Domain / Application / Infrastructure projects; `src/OpenPortal.Web` is the composition root (controllers, middleware, migrations, `ClientApp`); `OpenPortal.SharedKernel` holds `Result`, `Error`, `IClock`, `TextRules`. `src/OpenPortal.Client` is the standalone NuGet package other apps reference; `samples/OpenPortal.SampleApp` uses it.

Rules enforced by `tests/OpenPortal.Tests.Architecture` (NetArchTest) — a violation fails the build, so respect them:
- Domain references no EF Core / ASP.NET Core; Application references no EF Core and not its own Infrastructure.
- Identity, Content and Access never reference each other (Access gets user names through the host's `IUserDirectory` adapter and the admin check through `IAccessAdminAuthorization`). `OpenPortal.Client` references nothing of the portal.
- Only Infrastructure and Web use EF Core; DbContexts live only in Infrastructure. DB provider choice lives only in `Web/Persistence/DatabaseProviderSelector`.
- Module service implementations are `internal sealed`; the host resolves Application interfaces only. Each module exposes one public `AddXModule`. Because a missing registration is invisible until first call, `CompositionTests` resolves every module contract — add new contracts there.
- Controllers are `sealed`.

Conventions that span many files:
- **Operations return `Result`/`Result<T>`, not exceptions.** Domain entities validate *before* constructing (e.g. `Project.ValidateSlug`) so bad input is a 400, not an escaped `ArgumentException` → 500. Failures map to `application/problem+json` with an `errorCode` extension (`ProblemResults.FromResult`); every non-2xx must be problem+json.
- **Antiforgery on every unsafe request, including sign-in.** `ValidateAntiforgeryTokenFilter` is global and must `await ValidateRequestAsync`. Session cookie is HttpOnly; `XSRF-TOKEN` is script-readable and echoed as `X-XSRF-TOKEN`. Token is identity-bound, so the client drops its cached token after login/logout.
- **Authorization is checked twice**: `[Authorize(Policy = Policies.AdministratorOnly)]` on controllers and `RoleContentEditAuthorization` inside the Content service.
- **Public vs management DTOs are deliberately separate** (`ProjectDto` slug-addressed with no ids/draft flags; `ManagedProjectDto` for the editor). Don't merge them — `Public_project_payload_does_not_leak_editor_fields` guards it.
- **Password policy is served via `/api/auth/session`** and the client builds validation from it; don't duplicate the rules in the client.
- Each module has its own DbContext, schema and migrations under `Web/Persistence/Migrations/{Identity,Content}`. Migrate on startup only when `Database:MigrateOnStartup` is true (dev); production applies migrations as a separate step. SQLite can't order `DateTimeOffset`, hence the per-module `SqliteDateTimeOffsetCompatibility` converter.
- Integration tests use `WebApplicationFactory<Program>` on a temp SQLite file; don't add port-binding smoke tests.
- **SSO (Access module + `Web/Oidc`)**: OpenIddict, code flow + PKCE + refresh only. The access rule (app *active* and a direct or group grant; groups are flat) lives in `AccessQueries` and is enforced in `ConnectController` on authorize **and on every token request**; removing a grant/membership also revokes tokens. `ConnectController` and `POST /api/apps/announce` carry `[IgnoreAntiforgeryToken]`, which the global filter honours. The Identity cookie redirects to `/sign-in?returnUrl=` only for `/connect/*` (API paths stay 401). Apps announce themselves with `Access:ProvisioningKey` and stay *pending* (no OpenIddict client) until approved; approval returns the client secret once. Announced redirect URIs are only *proposed* for an approved app. Keys: Data Protection persisted to `DataProtection:KeysPath`; token keys from `Oidc:*` certificates (dev certs in Development, `Oidc:UseEphemeralKeys` in tests).
- `CreatedAtAction(nameof(GetAsync))` works because `SuppressAsyncSuffixInActionNames = false` in `Program.cs`.
- New migrations for Access: `dotnet ef migrations add <Name> --project src/OpenPortal.Web --context AccessDbContext --output-dir Persistence/Migrations/Access`. If a running instance locks `bin/Debug`, add `--configuration <other>`.

## Client (`src/OpenPortal.Web/ClientApp`)

React 19, Vite, Tailwind 4, **shadcn/ui** (new-york, neutral). Path alias `@/` → `src/` (tsconfig `paths` + Vite `resolve.alias`). The `BuildClientApp` csproj target builds it and copies `dist` into `wwwroot` (preserving `error.html`); `MapFallback` serves `index.html` but refuses `/api/*` so typos stay 404s. TanStack Query: the session query (`SESSION_QUERY_KEY`) has `staleTime: 0`; GETs retry twice, writes never retry.

### Component library: shadcn/ui only
- All primitives (Button, Input, Textarea, Checkbox, Label, Card, Table, Badge, Alert, AlertDialog, Dialog, DropdownMenu, Sheet, Tabs, Collapsible, Command (cmdk), Popover, Switch, Tooltip, ScrollArea, Separator, Skeleton, Sonner) live in `src/components/ui` and come from the shadcn CLI. Never hand-roll an input/button/table/dialog or paste input class strings; import from `@/components/ui/*`.
- Local edits to generated files are deliberate and small: `Button` defaults to `type="button"` (pass `type="submit"` explicitly; with `asChild` no type is set), `Badge` has extra `success` / `warning` variants, and `sonner.tsx` reads the theme from our own `useTheme` (no `next-themes`).
- Composites built only from those primitives: `FormField` (label + control + hint/error; injects `id`, `aria-invalid`, `aria-describedby` into its single child), `Section` (titled Card), `PageHeader`, `StatePanels` (`LoadingState`, `ErrorPanel`, `EmptyState`). Icons come from `lucide-react`. Access screens share `components/access/*` (`UserPicker`/`GroupPicker` comboboxes, `ApplicationStatusBadge`, `OnlineIndicator`, `SecretDialog`, `ApplicationFormDialog`, `UserAccessSheet`) and `lib/access.ts` (`useAccessRefresh` invalidates every access query after a change). `TooltipProvider` wraps the router in `App.tsx`. Dialog forms live in an inner component rendered inside `DialogContent`, so they start fresh on every opening (no reset effects).
- Tables: shadcn `Table`, actions column `text-right`, `size="sm"` buttons, wrapped by the component's own scroll container. Forms: `grid items-start gap-5` so rows stay aligned whether or not a field shows a hint or error. Form state is local `useState` + zod (`zodFieldErrors` in `lib/forms.ts`); react-hook-form was removed.

### Toast notifications
- Every user message (success, error, info, warning) is a toast: Sonner, mounted once as `<Toaster />` in `App.tsx`, **top-right**, `richColors`, close button. Position and styling are decided only in `components/ui/sonner.tsx`.
- Nothing except `hooks/useToast.ts` imports `sonner`. Use the hook (or the module-level `notify` outside components):

```tsx
const toast = useToast()
toast.success('Saved')                        // also: error, info, warning
toast.warning('Some fields need attention')
toast.fromError(err, 'Could not save')        // shows the server's problem+json detail
```

- Field-level validation stays inline under the field (via `FormField`); it is not a toast.
- Safety net: `App.tsx` installs a `MutationCache.onError` that toasts any failed mutation, so a failure cannot be silent. Mutations that report errors themselves set `meta: { handlesErrors: true }` and call `reportFormError` from `lib/forms.ts` (nudges on 400 field errors, toasts everything else). A 401 on a mutation toasts "session ended" and invalidates the session so the route guards redirect.

### Authentication flow and routing
There are **no anonymous pages** except `/sign-in`. The route tree is in `App.tsx`; guards are in `routes/guards.tsx`:
- `PublicOnlyRoute` → `AuthLayout` → `/sign-in`. A signed-in user is redirected to `location.state.from` or `/`.
- `ProtectedRoute` → `AppLayout` → `/` (dashboard + launchpad of the user's apps), `/account`, `/access-denied`, and `AdminRoute` (non-admins go to `/`) → `/admin/users`, `/admin/access` (tree: app → groups → members, app → direct users), `/admin/applications`, `/admin/groups`, `/admin/content/*`. Admin links sit in one "Administration" dropdown in the header.
- `PublicOnlyRoute` sends a signed-in user to `?returnUrl=` with a full page load when `lib/returnUrl.ts` accepts it (local `/connect/authorize` only); that is how sign-in started by another app returns to the OIDC endpoint. Vite proxies `/connect` too.
- While `useSession().isPending` the guards show a skeleton (never treat "not loaded" as "signed out"); a failed session fetch shows a retry panel; an anonymous visitor is redirected to `/sign-in` with the original URL saved.
- Sign-in does not navigate itself: it invalidates the session and `PublicOnlyRoute` redirects. Sign-out clears the query cache and goes to `/sign-in`.
- Client guards are UX only; the server still authorizes every endpoint.

### Login page
`SignInPage` inside `AuthLayout` (brand header with the favicon mark, form, small footer): a translucent shadcn card over `StarField`, plus `MagneticCursor` and a `ThemeToggle` fixed top-right.
- `StarField`: canvas of ~160 twinkling stars with a slight parallax against the pointer; colour from the `--foreground` token; pauses while the tab is hidden; one still frame under `prefers-reduced-motion`. It sizes itself with a `ResizeObserver` because the canvas can be zero-sized at mount (a width of 0 once produced `NaN` positions and invisible stars).
- `MagneticCursor`: a dot that tracks the pointer plus a lagging ring, with three states. *Free*: dot + ring. *Near* (within `NEAR_RANGE` = 120px of a control but not over it): a soft light, in the theme's own `--foreground` colour (no accent colour, to keep the portal minimal), spreads from the point where the pointer and the control are closest. The ring lights the arc facing the control (conic gradient, widening from a sliver to the full ring), and the control lights its edge and a soft fill around its nearest point (radial gradient, an overlay div so the control's own styles and focus ring are untouched). Strength follows a smoothstep of the distance; the lit point and facing direction are eased so they glide along the edge. The styles live in the `.magnetic-glow` / `.magnetic-ring` rules at the end of `index.css` and are driven by CSS custom properties set each frame. *Snapped* (over a link, button, input, textarea, select, label, checkbox or any `data-magnetic` element): both are fully lit, the ring morphs to that element's box and radius and the element is pulled a few px toward the pointer. Pulling uses the `translate` property, with the element's CSS transition suspended while pulled. Fine pointers only (`(hover: hover) and (pointer: fine)`); while mounted it adds `html.custom-cursor`, which hides the system cursor (rule at the end of `index.css`) and removes it on unmount. Motion is written straight to the DOM in one rAF loop, never through React state.
- `ThemeToggle` cross-fades and rotates lucide Sun/Moon icons.

### Localization (it / es / en / fr, extensible)
One source of truth: the ASP.NET Core resource files `src/OpenPortal.Web/Resources/Messages*.resx` (neutral `Messages.resx` = English, plus `Messages.<lang>.resx`). They translate **both** the server (`IStringLocalizer<Messages>`) and the client, which has no translation files and no i18n library: `GET /api/i18n/messages?lang=xx` serves the strings, `GET /api/i18n/languages` the offered list.
- **Adding a language** (e.g. `de`): a line in `Localization:Languages` (`appsettings.json`) + `Messages.de.resx`. No code. `npm run lint` (`scripts/check-i18n.mjs`) fails if a satellite file lacks a key, or if the client uses a literal key that is not in `Messages.resx`.
- **Server**: `UseRequestLocalization` (before `UseExceptionHandler`) reads `?lang=` then `Accept-Language`; the client sends `Accept-Language` on every request. `ProblemDetailsFactory` translates `detail` by `error.<errorCode>` and `title` by `problem.title.<type>`, falling back to the English `Error.Description`; `errorCode` is never translated. A new `Error` therefore needs an `error.<code>` key. DataAnnotations `ErrorMessage` on request contracts are keys (`validation.*`), resolved in `InvalidModelStateResponseFactory`, so modules stay free of localization. Domain/Application never reference localization; the allowed-language check goes through `ILanguageCatalog` (implemented by the host's `LanguageCatalog`).
- **Persistence**: `ApplicationUser.Language` (nullable, `SetLanguage`), exposed in the session and profile DTOs, saved by `PUT /api/account/language`. Anonymous visitors keep the choice in `localStorage` (`openportal-lang`), else the browser language, else the default.
- **Client**: `src/i18n/store.ts` is framework-free (`translate`, `formatDate`, `formatList`, `setLanguage`) so `ApiError`, `notify` and the zod schemas can translate; components use `useI18n()` (`t`, `language`, `languages`). Keys are dotted strings with `{param}` placeholders and plurals as `key_one` / `key_other` (pass `{ count }`). Zod schemas and `lib/passwordPolicy.ts` take `t` and are built in `useMemo(..., [t])`. Dates/lists use `Intl`, never `toLocaleDateString()`. `<LanguageSwitcher />` sits next to `ThemeToggle` in both layouts; `<LanguageSync />` (in `App.tsx`) applies the profile language after sign-in and saves the current one when the profile has none. `I18nProvider` holds the app back until the first language is loaded.

### Theme
Light is `:root`; dark is the `dark` class on `<html>` (`@custom-variant dark`). `ThemeProvider` / `useTheme` (`components/theme-provider.tsx`) persist the choice in localStorage (`openportal-theme`, guarded by try/catch) and fall back to the OS preference; an inline script in `index.html` applies it before first paint. Use theme tokens (`bg-background`, `text-muted-foreground`, `bg-success/15`), not raw colours.

### Layout
`AppLayout` = `div.flex.min-h-screen.flex-col` → sticky header (logo, `NavItem` links, theme toggle, mobile menu, user dropdown with Account / Sign out) → `main.flex-1` → footer. `flex-1` on `main` is what keeps the footer at the bottom on short pages.

### Breaking changes / migration notes
- The public portfolio pages and routes (home, `/projects`, `/projects/:slug`) and `publicContentApi` are removed from the client; `/` is now the dashboard. The Content module (profile/projects) stays as a sample admin module and its public `/api/content` endpoints are untouched, but nothing in the client calls them.
- `components/ui.tsx` and `routes/RootLayout.tsx` are gone (replaced by `components/ui/*`, `AppLayout`, `AuthLayout`). `hooks/useRetryableError` became `lib/errors.ts`, keeping only `describeError` and `traceIdOf`.
- Dark mode is class-based, and the default follows the OS; the old dark-by-default CSS is gone.
- Dependencies added: `radix-ui`, `sonner`, `lucide-react`, `class-variance-authority`, `clsx`, `tailwind-merge`, `tw-animate-css`. Removed: `react-hook-form`, `@hookform/resolvers`.
