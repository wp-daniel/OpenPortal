<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/brand/openportal-logo-white.svg">
    <img src="docs/brand/openportal-logo.svg" alt="OpenPortal" width="280">
  </picture>
</p>

# Architecture

OpenPortal is a modular monolith: one deployable ASP.NET Core host, a React client, and three business modules
(Identity, Content, Access) that are isolated by convention and enforced by tests. It is also an OpenID Connect
provider, so other applications can sign their users in through it.

The shape exists to make two specific promises cheap to keep:

1. **The modules do not know about each other.** Identity, Content and Access could each be extracted, replaced
   or tested alone; where one needs something from another, the host supplies it through a port.
2. **The database engine is a host-level decision.** No domain, application or module-infrastructure project
   references a provider, so moving from SQLite to PostgreSQL touches one file.

Both promises are checked by `OpenPortal.Tests.Architecture`. If a rule below is broken, a test fails and
names the rule, rather than waiting for review to notice.

---

## Solution layout

```
OpenPortal.slnx
src/
  OpenPortal.SharedKernel/              Result, Error, IClock, TextRules
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
  OpenPortal.Client/                    NuGet package for applications: OIDC sign-in + announcement
  OpenPortal.Web/                       composition root: controllers, middleware, migrations, ClientApp, Oidc/
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

**Authorisation** is applied twice on purpose. The `[Authorize(Policy = Policies.AdministratorOnly)]`
attribute is the first gate, and `RoleContentEditAuthorization` re-checks inside the Content service. An
abstraction reachable from more than one pipeline cannot depend on each caller remembering to guard it.

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
`IUserDirectory` adapter (backed by Identity's `IUserLookupService`), and "is the caller an administrator"
through `IAccessAdminAuthorization` — the same seam as Content's `IContentEditAuthorization`.

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

**Keys.** Data Protection keys are persisted to `DataProtection:KeysPath` so sessions survive restarts and
can be shared between instances (protect them at rest in production, e.g. `ProtectKeysWithCertificate`).
Token signing and encryption use development certificates in Development, ephemeral keys in tests
(`Oidc:UseEphemeralKeys`), and configured PKCS#12 certificates everywhere else; startup fails without them.

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

Three `DbContext` instances, one per module (Identity, Content, Access). Each module owns its schema and its
migrations; no context knows the others exist, and the Access context also holds the OpenIddict tables.

**Provider selection** lives only in `DatabaseProviderSelector.Configure`. Modules register their contexts
through a callback that receives a provider-agnostic `DbContextOptionsBuilder`:

```csharp
services.AddContentModule(options => DatabaseProviderSelector.Configure(options, provider, connectionString));
```

Setting `Database:Provider` to `PostgreSql` currently throws at startup with an instruction to add Npgsql,
rather than failing at the first query.

**SQLite compatibility.** SQLite cannot order by `DateTimeOffset`, so every context applies a value converter
that maps to UTC ticks. This is provider-specific and lives in the Infrastructure layer, which is the only
layer allowed to know the provider.

**Migrations** are checked in under `src/OpenPortal.Web/Persistence/Migrations/{Identity,Content,Access}`. They run
at startup only when `Database:MigrateOnStartup` is true (development); in production they belong in a
separate, ordered deployment step, because several instances migrating concurrently is a race.

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
| `OpenPortal.Tests.Integration` | the whole stack over HTTP | `WebApplicationFactory<Program>` on a temporary SQLite file |

Integration tests boot the real host, apply the real migrations and exercise the real cookie and antiforgery
behaviour. There are no port-binding or out-of-process smoke tests: a test that starts a server, curls a
port and leaves a process behind proves less than one that asserts on a response, and fails for reasons that
have nothing to do with the code under test.
