<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/brand/openportal-logo-white.svg">
    <img src="docs/brand/openportal-logo.svg" alt="OpenPortal" width="280">
  </picture>
</p>

# Architecture

OpenPortal is a modular monolith: one deployable ASP.NET Core host, a React client, three business modules
(Identity, Content, Access) and an Audit module, isolated by convention and enforced by tests. It is also an OpenID Connect
provider, so other applications can sign their users in through it.

The shape exists to make two specific promises cheap to keep:

1. **The modules do not know about each other.** Identity, Content and Access could each be extracted, replaced
   or tested alone; where one needs something from another, the host supplies it through a port.
2. **The database engine is a host-level decision.** No domain, application or module-infrastructure project
   references a provider; the host picks SQLite or PostgreSQL in one file, and each provider has its own
   migrations.

Both promises are checked by `OpenPortal.Tests.Architecture`. If a rule below is broken, a test fails and
names the rule, rather than waiting for review to notice.

---

## Solution layout

```
OpenPortal.slnx
src/
  OpenPortal.SharedKernel/              Result, Error, IClock, TextRules, IAuditTrail
  Modules/
    Identity/
      OpenPortal.Identity.Domain/          ApplicationUser, Roles
      OpenPortal.Identity.Application/     IAuthenticationService, IAccountService, IUserAdministrationService
      OpenPortal.Identity.Infrastructure/  IdentityDbContext, services, AddIdentityModule
    Content/
      OpenPortal.Content.Domain/           Profile, Project, Technology
      OpenPortal.Content.Application/      IPublicContentService, IContentManagementService, DTOs
      OpenPortal.Content.Infrastructure/   ContentDbContext, services, AddContentModule
    Access/
      OpenPortal.Access.Domain/            PortalApplication, Group, ApplicationUserGrant, ApplicationGroupGrant
      OpenPortal.Access.Application/       IApplicationRegistryService, IGroupService, IAccessAdministrationService,
                                           IAccessEvaluator, IApplicationAnnouncementService, host ports
      OpenPortal.Access.Infrastructure/    AccessDbContext (+ OpenIddict tables), services, AddAccessModule
    Audit/
      OpenPortal.Audit.Domain/             AuditEntry
      OpenPortal.Audit.Application/        IAuditLogService, host ports (IAuditRequestContext, IAuditLogAuthorization)
      OpenPortal.Audit.Infrastructure/     AuditDbContext, IAuditTrail implementation, retention sweep, AddAuditModule
  OpenPortal.Migrations.PostgreSql/     the PostgreSQL migrations of every module
  OpenPortal.Client/                    NuGet package for applications: OIDC sign-in + announcement
  OpenPortal.Web/                       composition root: controllers, middleware, SQLite migrations, ClientApp, Oidc/,
                                        Security/ (headers, rate limits, proxy), Health/, Auditing/
samples/
  OpenPortal.SampleApp/                 minimal application signing in through the portal
tests/
  OpenPortal.Tests.Unit/                domain rules
  OpenPortal.Tests.Architecture/        dependency rules
  OpenPortal.Tests.Integration/         the real host over HTTP
```

---

## The dependency rules

```
Web  ──────────────►  *.Infrastructure  ──►  *.Application  ──►  *.Domain  ──►  SharedKernel
                            │                                              ▲
                            └──────────────────────────────────────────────┘
```

| Rule | Enforced by |
| --- | --- |
| Domain never references EF Core or ASP.NET Core | `Domain_projects_do_not_reference_EntityFrameworkCore`, `..._AspNetCore` |
| Application never references EF Core | `Application_projects_do_not_reference_EntityFrameworkCore` |
| Application never references its own Infrastructure | `Application_projects_do_not_reference_their_own_infrastructure` |
| Identity never references Content, and vice versa | `Identity_does_not_reference_Content`, `Content_does_not_reference_Identity` |
| Access references neither Identity nor Content, and neither references Access | `Access_does_not_reference_Identity_or_Content`, `Identity_and_Content_do_not_reference_Access` |
| Identity, Content and Access record audit events through the shared kernel and never reference Audit; Audit references none of them | `Modules_record_audit_events_without_referencing_the_Audit_module` |
| The client package references nothing of the portal | `The_client_package_stands_alone` |
| SharedKernel references no framework and no module | `SharedKernel_references_no_framework_or_module_assembly` |
| DbContexts and repositories live only in Infrastructure | `Only_the_web_host_and_infrastructure_projects_use_EntityFrameworkCore`, `DbContexts_live_only_in_infrastructure_projects` |
| Service implementations are `internal` | `Module_services_are_hidden_behind_their_application_interfaces` |
| One public `AddXModule` per module | `Modules_are_composed_through_a_single_public_extension_point` |
| Controllers are `sealed` | `Controllers_are_sealed` |

### Why service implementations are internal

Every module service is `internal sealed`. The host cannot name the concrete type, so it must resolve the
interface published by the Application layer — which is what makes the module substitutable. The flip side
is that a missing registration is invisible until an endpoint is first called, so
`OpenPortal.Tests.Integration.CompositionTests` resolves every module contract from the real composition
root.

---

## Error handling

Two conventions, one shape.

**In the domain and application layers**, operations return `Result<T>` or `Result`. A failure carries an
`Error` with a stable `Code` (for example `content.slug_already_in_use`), a human-readable description and a
severity. Domain methods use the same pattern, because an entity that throws on invalid input turns a bad
request into a 500: `Project.ValidateSlug` is called before the constructor rather than by catching its
`ArgumentException`.

**At the edges**, `ProblemResults.FromResult` maps a failed `Result` to RFC 9457 `application/problem+json`
with the error code as the `errorCode` extension.

Every non-2xx response is `application/problem+json`, including the ones MVC produces before a controller
runs. That last part needed `ProblemDetailsJsonOutputFormatter`, inserted at the head of
`MvcOptions.OutputFormatters`: an `[ApiController]` action rejects a malformed body through
`InvalidModelStateResponseFactory`, and neither that path nor the default model-state path sets the
content type on its own.

`ProblemDetailsJsonOutputFormatter` is registered as an `IConfigureOptions<MvcOptions>` rather than inside
`AddMvcOptions` because it needs MVC's `JsonOptions`, which are only available from the container.

---

## Authentication and authorisation

Cookie authentication, JSON API, no server-rendered login form. `OnRedirectToLogin` and
`OnRedirectToAccessDenied` answer 401 and 403 instead of redirecting, because there is no HTML login page for
the redirect to reach.

**Antiforgery.** Every unsafe request is validated, including sign-in.

- The session cookie is `HttpOnly` and never readable by script.
- The antiforgery cookie is `XSRF-TOKEN` and *is* readable: it is a per-session nonce, not a credential.
- The client echoes it in the `X-XSRF-TOKEN` header.
- `GET /api/auth/antiforgery` issues a token when the cookie is absent.

The token is bound to the signed-in identity, so the client discards its cached token after login and after
logout. Without that, the first write following a sign-in fails with 400 and looks like a bug in the form.

`ValidateAntiforgeryTokenFilter` is a global filter and must `await` `ValidateRequestAsync`; forgetting the
await silently disables validation for every endpoint.

**Authorisation** is applied twice on purpose. `[RequirePortalPage(...)]` (or, for what is never
delegated, `[Authorize(Policy = Policies.AdministratorOnly)]`) is the first gate, and each module re-checks
inside its services through a host adapter (`IUserAdministrationAuthorization`, `IAccessAdminAuthorization`,
`IContentEditAuthorization`, all in `Web/Authorization/ModuleAuthorization.cs`). An abstraction reachable
from more than one pipeline cannot depend on each caller remembering to guard it.

**Page permissions.** The administration pages can be granted to groups, like applications. The pages are
declared once, in `Web/Authorization/PortalPages.cs` (key, label key, area key); administrators open every
page without a grant, and a group granted a page gives all its members full use of that page.

- Endpoints carry `[RequirePortalPage(page, ...)]`: the caller passes when they hold *any* listed page. A
  read shared by several screens lists them all (listing users is allowed to the users, groups and access
  pages, because those screens pick users), the writes list only their own page.
- `PortalPageAuthorizationHandler` reads the caller's pages from the database once per request
  (`CurrentPagePermissions`); nothing is cached in the cookie, so removing a grant or a membership takes
  effect on the next request.
- Modules do not know pages. They name an *operation* (`AccessOperation`, `UserAdministrationOperation`,
  `ContentArea`) and the host maps it to the same page lists as the endpoints.
- Grants live in Access (`PageGroupGrants`, keyed by page key and group, cascading with the group). Access
  validates keys against the host's `IPortalPageCatalog`; a key the host no longer declares is ignored.
- Never delegated: the page permissions themselves (`/api/admin/page-permissions`, `AdministratorOnly`),
  granting or removing the administrator role, and changing an administrator's account, password or
  picture (`AdministrationGuard` in Identity, `identity.administrator_required`). A holder of the groups page
  can still add themselves to another group; delegate it accordingly.
- The session carries `user.pages` (every page for an administrator, via the host's `IUserPageSource`), from
  which the client builds the sidebar and guards routes.

Adding a page: declare it in `PortalPages` (and `All`), put `[RequirePortalPage]` on its endpoints, give the
client route `handle.page` and the sidebar link `page` with the same key, and add the label keys to the
resx files. It appears on the page permissions screen and in the group dialog by itself. `PagePermissionTests` fails
when an `api/admin`/`api/manage` endpoint has neither a page nor `AdministratorOnly`, when a page is used but
not declared (or declared but unused), or when its labels are not translated.

**The password policy is published, not restated.** `/api/auth/session` returns the policy read from
`IOptions<IdentityOptions>.Password`, and it does so for anonymous callers too, because the change-password
form needs it before anyone signs in. The client builds its validation from that payload, so there is no
second copy of the rules to drift.

Two consequences are load-bearing:

- `SessionDto.AnonymousFor` takes the policy as an argument instead of defaulting it. A fabricated policy
  could only ever disagree with the server, and the disagreement would reach the user as an unexplained 400.
- The client's placeholder is `UNAVAILABLE_PASSWORD_POLICY`, an obviously-unreal policy compared by
  reference. Until the session loads, the forms skip complexity checks entirely rather than enforcing a
  guess. A permissive-looking default would have been worse than no default: indistinguishable from a real
  configuration, and it would reject passwords the server accepts.

---

## Single sign-on for other applications (Access module)

The portal is an OpenID Connect provider built on OpenIddict. Only the authorization-code flow with PKCE and
refresh tokens is enabled; clients are confidential.

**Who owns what.** `OpenPortal.Access` owns applications, groups and grants, and the OpenIddict client
store (its tables live in `AccessDbContext`, so a client and its `PortalApplication` commit together). The
host owns the OpenIddict *server*: endpoints, keys and `Oidc/ConnectController`, because they depend on the
authentication stack and the environment. Access stores user ids only; names come through the host's
`IUserDirectory` adapter (backed by Identity's `IUserLookupService`), and "may the caller do this"
through `IAccessAdminAuthorization` — the same seam as Content's `IContentEditAuthorization`. Access also
stores the page grants (see *Page permissions* above) against the host's `IPortalPageCatalog`.

**The access rule** lives in one place (`AccessQueries`): a user may open an application when it is
*active* and the user holds a direct grant or belongs to a group that holds one. Groups are flat. The rule is
applied

- in `/connect/authorize`: a user without access is sent to the SPA's `/access-denied` page and no code is
  issued;
- in `/connect/token`, on the code exchange **and every refresh**: withdrawn access yields `invalid_grant`,
  so an application loses the session within the access-token lifetime even if nothing else happens;
- when a grant, membership, group or application goes away, the affected users' OpenIddict authorizations and
  tokens are revoked as well.

**Sign-in from another application.** `/connect/authorize` authenticates with the Identity cookie. When there
is none, the cookie handler's `OnRedirectToLogin` redirects *only for `/connect/*`* to
`/sign-in?returnUrl=…` (every `/api` path still gets a bare 401). After sign-in, `PublicOnlyRoute` does a
full page load back to the return URL, which `lib/returnUrl.ts` restricts to a local `/connect/authorize`
path so it cannot be used as an open redirect.

**Antiforgery** does not apply to `ConnectController` and to `POST /api/apps/announce`: they are reached by
cross-site redirect or called server-to-server and authenticate through their own protocol (PKCE and client
secret; the provisioning key). Both carry `[IgnoreAntiforgeryToken]`, which the global filter honours; every
other unsafe endpoint still requires the token (an integration test guards that).

**Discovery of deployed applications.** An application using `OpenPortal.Client` announces itself at start-up
and every few minutes with the provisioning key. A new client id becomes a *pending* application with no
OpenIddict client, so it cannot sign anyone in until an administrator approves it; approval creates the
client and returns its secret once (only a hash is stored). Later announcements are a heartbeat ("last seen")
and may *propose* new redirect URIs, which an administrator must apply: accepting them on the application's
word would let whoever holds the shared provisioning key redirect sign-ins anywhere.

**Roles and groups in tokens.** An application defines the roles it understands (`ApplicationRole`: an
immutable key such as `sales`, plus a name for administrators). Roles are assigned *on grants*: a user grant
or a group grant carries role keys, so taking the grant away takes its roles too, and a user's roles in an
application are the union over their own grant and their groups' grants (`AccessQueries.RolesAsync`). The
token endpoint recomputes them, with the access check, at every code exchange and refresh, and emits them as
`role` claims when the application asked for the `roles` scope; the identity token carries them, so
`OpenPortal.Client` (role claim type `role`) makes `User.IsInRole` work. Groups are the portal's business by
default: an application receives `groups` claims only if its `GroupClaims` setting is `granted` (the user's
groups that grant it) or `all`, and it asked for the `groups` scope. An application announcing itself may
declare its roles: a pending one takes them whole, an approved one only gains new ones, because renaming or
removing a role an administrator may have assigned is the administrator's call. Removing a role strips it from
every grant. Clients approved before roles existed get the new scope permissions at startup
(`OidcClientUpgradeService`).

**Keys.** Data Protection keys are persisted to `DataProtection:KeysPath` so sessions survive restarts and
can be shared between instances (protect them at rest in production, e.g. `ProtectKeysWithCertificate`).
Token signing and encryption use development certificates in Development, ephemeral keys in tests
(`Oidc:UseEphemeralKeys`), configured PKCS#12 certificates where given, or self-signed certificates the portal
creates once in `Oidc:CertificatesPath` (a container volume); startup fails without any of them.

---

## Audit log (Audit module)

Every module records what it changes, and the host records the OpenID Connect sign-ins, through one port in
the shared kernel: `IAuditTrail.RecordAsync(AuditEvent)`. An event is an action code (`user.created`,
`access.group_roles_changed`), an outcome, a target, optional facts (`Details`, never secrets) and, when the
caller is not the signed-in user (a sign-in, an application's server), the actor. The Audit module implements
the port and stores entries; the host's `IAuditRequestContext` adds the caller, their address and the request's
trace id (the `traceId` an error response shows), so a reported failure can be found in the log.

The design choices, in the order they matter:

- **A port in the shared kernel, not a reference to Audit.** The modules stay independent of each other; an
  architecture test checks it. Each module declares its action codes as constants (`IdentityAuditActions`,
  `AccessAuditActions`, `ContentAuditActions`, `OidcAuditActions`), and a test checks every one has a label.
- **Recorded after the change is committed, never inside a transaction**, so a rolled-back change is never
  reported, and SQLite's single writer is not held by two connections at once.
- **Best effort.** A failed write is logged and swallowed: the change already happened, and a 500 would invite
  a retry of something that succeeded. The entry clips over-long values rather than refusing them.
- **Snapshots, not references.** Labels (an email, an application name) are copied into the entry, so the log
  reads the same after the subject is renamed or deleted.
- **Only real changes.** Idempotent repeats (granting what is granted) and announcement heartbeats record
  nothing; a privilege change (`user.administrator_granted`) is its own entry rather than a detail of an edit.

Reading the log is the `audit` portal page, so it can be delegated to auditors. Entries older than
`Audit:RetentionDays` are deleted once a day by `AuditRetentionService`. The same request also gives each
account its `LastSignInAtUtc`, which the users list shows and filters on (inactive for 90 days).

---

## Hardening

The protections every request passes through, in pipeline order (`Program.cs`):

1. **Forwarded headers** (`ReverseProxy:*`), first, so everything after sees the client's address and scheme.
   Off by default: trusting `X-Forwarded-For` from anyone would let a caller pick their own address.
2. **Security headers** (`SecurityHeadersMiddleware`), set as the response starts so error pages and static
   files get them too: a Content-Security-Policy allowing this origin only (`style-src 'unsafe-inline'` is the
   one concession, for the toast library's injected stylesheet; scripts are never inline, which is why the
   theme bootstrap is `public/boot.js`), `frame-ancestors 'none'` and `X-Frame-Options: DENY` against
   clickjacking the sign-in page, `no-referrer`, a closed `Permissions-Policy`, and `Cache-Control: no-store` on
   API answers. The OpenID Connect endpoints get only the anti-framing part: they answer other applications.
3. **Rate limits** (`RateLimitingSetup`), after authentication so a signed-in caller is counted as themselves:
   a generous global limit on `/api` and `/connect`, and stricter per-address ones on password checks
   (complementing per-account lockout, which cannot stop one password tried against many accounts) and on
   the machine endpoints (token, announcements). A refusal is 429 problem+json with `Retry-After`.
4. **Health** (`/health/live`, `/health/ready`), outside the limits; readiness checks each module's database
   and reports pending migrations as `Degraded`.

---

## Public versus management projections

`ProjectDto` and `ManagedProjectDto` are separate types on purpose.

| | `ProjectDto` (public) | `ManagedProjectDto` |
| --- | --- | --- |
| Addressed by | slug | row id |
| Carries `IsPublished`, `Position`, `UpdatedAtUtc` | no | yes |
| Served to | anonymous readers | administrators only |

The editor cannot publish, reorder or delete without the id and the published flag, and neither belongs in a
public payload: publishing a draft flag and a row id to anonymous readers would let anyone enumerate
unpublished work. `Public_project_payload_does_not_leak_editor_fields` guards the split.

---

## Persistence

Four `DbContext` instances, one per module (Identity, Content, Access, Audit). Each module owns its schema and
its migrations; no context knows the others exist, and the Access context also holds the OpenIddict tables.

**Provider selection** lives only in `DatabaseProviderSelector.Configure`. Modules register their contexts
through a callback that receives a provider-agnostic `DbContextOptionsBuilder`:

```csharp
services.AddContentModule(options => DatabaseProviderSelector.Configure(options, provider, connectionString));
```

`Database:Provider` is `Sqlite` (the default) or `PostgreSql`. No query is provider-specific: text search
compares upper-cased values on both sides rather than relying on `LIKE`, whose case sensitivity differs between
the two engines.

**SQLite compatibility.** SQLite cannot order by `DateTimeOffset`, so every context applies a value converter
that maps to UTC ticks. This is provider-specific and lives in the Infrastructure layer, which is the only
layer allowed to know the provider.

**Migrations** exist once per provider, because a migration is written for one engine's column types: SQLite's
under `src/OpenPortal.Web/Persistence/Migrations/{Identity,Content,Access,Audit}`, PostgreSQL's in their own
assembly, `OpenPortal.Migrations.PostgreSql`, which the selector names as the migrations assembly. CI checks
that neither set lags behind the model (`has-pending-model-changes`). Migrations run at startup only when
`Database:MigrateOnStartup` is true (development, single-instance SQLite); in production they belong in a
separate, ordered deployment step, because several instances migrating concurrently is a race:
`OpenPortal.Web --migrate` applies them (with roles and the bootstrap administrator) and exits.

---

## The client

`src/OpenPortal.Web/ClientApp`, React 19 with Vite and Tailwind 4. No Razor, no view engine: the host serves
a JSON API plus the compiled Vite output.

**One origin, on purpose.** In development `Microsoft.AspNetCore.SpaProxy` forwards to the Vite dev server,
so the browser always talks to the ASP.NET origin and the session cookie stays first-party. The proxy is
wired automatically by the package's hosting startup from the `SpaRoot` / `SpaProxyLaunchCommand` /
`SpaProxyServerUrl` properties in the csproj; there is nothing to call in `Program.cs`.

**Build integration.** `BuildClientApp` in `OpenPortal.Web.csproj` runs `npm run build` before the .NET
build and copies `ClientApp/dist` into `wwwroot`. It is incremental on the client sources' timestamps, and
`-p:SkipClientBuild=true` disables it. The copy leaves `error.html` in place: that page is rendered by the
status-code middleware, not by the client.

**Routing.** `MapFallback` serves `index.html` so deep links and hard refreshes work, but explicitly refuses
paths under `/api`. Without that exclusion a typo in a client call would be answered with a 200 carrying
HTML, and the caller would report a JSON parse error instead of a 404 it could act on.

**Data.** TanStack Query with one query key per resource. The session is a single query under `['session']`
with `staleTime: 0`, so signing in or out updates every screen at once and a cookie rotated by another tab is
never served from cache. GETs retry twice; writes never retry, because a retried write can duplicate a side
effect the server already committed.

**Errors.** `ApiError` turns any non-2xx into a rejection carrying the problem document. Without it `fetch`
would resolve on 4xx and 5xx, and no error path in the app would be reachable.

---

## Testing

| Project | What it covers | How |
| --- | --- | --- |
| `OpenPortal.Tests.Unit` | domain rules, validation, normalisation | plain xUnit, no host |
| `OpenPortal.Tests.Architecture` | the dependency rules above | assembly references and type shapes |
| `OpenPortal.Tests.Integration` | the whole stack over HTTP | `WebApplicationFactory<Program>` on a temporary SQLite file, or a throwaway PostgreSQL database when `OPENPORTAL_TEST_POSTGRES` is set (CI runs both) |

Integration tests boot the real host, apply the real migrations and exercise the real cookie and antiforgery
behaviour. There are no port-binding or out-of-process smoke tests: a test that starts a server, curls a
port and leaves a process behind proves less than one that asserts on a response, and fails for reasons that
have nothing to do with the code under test.
