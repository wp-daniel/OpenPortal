<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/brand/openportal-logo-white.svg">
    <img src="docs/brand/openportal-logo.svg" alt="OpenPortal" width="420">
  </picture>
</p>

<p align="center">
  <strong>The application shell your next ASP.NET Core app signs in through.</strong><br>
  Users, sessions, a role-gated admin area and an OpenID Connect provider with per-app access — ready to build on.
</p>

<p align="center">
  <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-black"></a>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-black">
  <img alt="React 19" src="https://img.shields.io/badge/React-19-black">
  <img alt="OpenID Connect" src="https://img.shields.io/badge/OpenID%20Connect-provider-black">
  <a href="https://github.com/wp-daniel/OpenPortal/wiki"><img alt="Wiki" src="https://img.shields.io/badge/docs-wiki-black"></a>
</p>

<p align="center">
  <a href="#quick-start">Quick start</a> ·
  <a href="#features">Features</a> ·
  <a href="#connecting-another-application">Single sign-on</a> ·
  <a href="ARCHITECTURE.md">Architecture</a> ·
  <a href="https://github.com/wp-daniel/OpenPortal/wiki">Wiki</a>
</p>

---

OpenPortal is a reusable base for any ASP.NET Core application — a CRM, a back office, an internal tool. It
gives you the parts every such application needs and nobody wants to write twice: accounts and sessions, an
administration area, and a single place where other applications send their users to sign in and where you
decide who may open what.

It is an ASP.NET Core 10 API with a React 19 client (Vite, Tailwind 4, shadcn/ui), ASP.NET Core Identity and
OpenIddict, on SQLite by default, organised as a modular monolith whose boundaries are enforced by tests.

## Features

**Identity**
- Sign-in with lockout, sessions in an `HttpOnly` cookie, antiforgery on every unsafe request (sign-in too)
- User administration with server-side search and filters: names, phone, job title, company, department, address
- Profile pictures, cropped and scaled in the browser, validated by content on the server
- A password policy defined once on the server and published to the client, so forms never disagree with it

**Single sign-on**
- OpenID Connect provider (authorization code + PKCE + refresh tokens)
- Per-application access for users and for groups; withdrawing access revokes tokens and is re-checked on every refresh
- Applications announce themselves and wait for approval; a NuGet package (`OpenPortal.Client`) does the wiring
- A launchpad on the dashboard with the applications each user may open

**Experience**
- English, Italian, Spanish and French, with the language saved per user; one `.resx` per language translates
  both server and client
- Light and dark themes, collapsible sidebar, toasts for every message, accessible shadcn/ui components
- Every error is `application/problem+json` with a stable `errorCode`

**Engineering**
- Modular monolith (Identity, Access, Content) with dependency rules checked by architecture tests
- Operations return `Result` instead of throwing; bad input is a 400, never a 500
- Integration tests over the real host on a temporary SQLite file
- The database provider is one file away from PostgreSQL

## Quick start

You need the **.NET SDK 10.0.400** (pinned in `global.json`) and **Node.js 22** or newer.

```bash
dotnet tool restore
dotnet run --project src/OpenPortal.Web
```

That single command builds the client, builds the host, applies migrations and starts listening on
`https://localhost:7137`. In development the Vite dev server is proxied through the ASP.NET origin, so the
browser only ever talks to one host and the session cookie stays first-party.

Every page except `/sign-in` requires an account, so create the first administrator:

```bash
dotnet user-secrets --project src/OpenPortal.Web set "BootstrapAdmin:Enabled" "true"
dotnet user-secrets --project src/OpenPortal.Web set "BootstrapAdmin:Email" "you@example.com"
dotnet user-secrets --project src/OpenPortal.Web set "BootstrapAdmin:Password" "<a strong password>"
```

Restart and sign in. Users, groups, applications and access are under **Administration** in the sidebar.
Turn the bootstrap back off once the account exists; it only creates a missing account, so it is harmless,
but it should not stay enabled in a deployment.

Credentials belong in user secrets, never in a committed `appsettings*.json`.

### Useful variations

```bash
# Backend only, without building the client.
dotnet run --project src/OpenPortal.Web -p:SkipClientBuild=true

# Work on the client alone with fast refresh.
cd src/OpenPortal.Web/ClientApp && npm run dev

# Type-check and build the client; lint it (also checks that every language has every string).
cd src/OpenPortal.Web/ClientApp && npm run build
cd src/OpenPortal.Web/ClientApp && npm run lint
```

## Connecting another application

The portal is an OpenID Connect provider. Another ASP.NET Core application signs its users in through it, and
the portal decides who may:

1. Reference `src/OpenPortal.Client` and call `builder.Services.AddOpenPortalAuthentication(builder.Configuration)`
   plus `app.MapOpenPortalSignOut()`. Configure the `OpenPortal` section (`Authority`, `ClientId`, `BaseUrl`,
   and in user secrets `ProvisioningKey`).
2. On start-up the application announces itself and appears under **Administration → Applications** as
   *waiting for approval*. (Or register it by hand there.)
3. Approve it: the portal shows the client secret once. Put it in the application's `OpenPortal:ClientSecret`.
4. Under **Groups** put users in groups and switch the application on for the group, or give it to single
   users under **Access**, which shows the whole tree: application → groups → members, application → direct
   users.

A user without access is stopped at the portal. Withdrawing access revokes the application's tokens, and the
token endpoint re-checks access on every refresh, so the user cannot sign in again and the tokens stop working
within the access token lifetime (10 minutes by default). The application's own session is a local cookie that
`OpenPortal.Client` does not refresh by itself: a user already inside keeps it until it expires or they sign out.
[`samples/OpenPortal.SampleApp`](samples/OpenPortal.SampleApp) shows the whole thing; the
[wiki](https://github.com/wp-daniel/OpenPortal/wiki/Connecting-an-Application) walks through connecting any app.

## Tests

```bash
dotnet test                                  # everything
dotnet test tests/OpenPortal.Tests.Unit
dotnet test tests/OpenPortal.Tests.Architecture
dotnet test tests/OpenPortal.Tests.Integration
```

Integration tests boot the real host against a temporary SQLite file. Nothing binds a fixed port and no
process is left behind.

## Publishing

```bash
dotnet publish src/OpenPortal.Web -c Release -o publish
```

The client is built as part of this, so `publish/wwwroot` contains the SPA. In production:

- keep `Database:MigrateOnStartup` at `false` and apply migrations as a separate ordered step
  (`dotnet ef database update`), because several instances migrating at once is a race;
- keep `Identity:Cookie:RequireSecure` at `true` and terminate TLS in front of the app;
- provide the token signing and encryption certificates (`Oidc:*`) and a shared `DataProtection:KeysPath`;
- supply `ConnectionStrings:OpenPortal` and, if you move off SQLite, add the Npgsql package and wire
  `UseNpgsql` in `DatabaseProviderSelector`.

## Layout

| Path | Contents |
| --- | --- |
| `src/OpenPortal.SharedKernel` | `Result`, `Error`, `IClock`, `TextRules` |
| `src/Modules/Identity` | accounts, sessions, roles, user details and avatars — Domain / Application / Infrastructure |
| `src/Modules/Access` | applications, groups and access grants, plus the OpenID Connect client store |
| `src/Modules/Content` | a sample admin module (profile and projects) |
| `src/OpenPortal.Client` | NuGet package an application references to sign in through the portal |
| `samples/OpenPortal.SampleApp` | a minimal application that uses `OpenPortal.Client` |
| `src/OpenPortal.Web` | composition root, controllers, OIDC endpoints, middleware, migrations, the client |
| `tests` | unit, architecture and integration suites |
| `docs/brand` | logo, symbol and app icon |

[`ARCHITECTURE.md`](ARCHITECTURE.md) explains why it is arranged this way, what each boundary buys, and which
test enforces it.

## How it is put together

Three things are worth knowing before changing anything.

**Operations return `Result`, they do not throw.** A failure carries a stable error code that reaches the
client as the `errorCode` extension of a problem+json response. Domain entities validate before constructing
rather than throwing, because a rejected input must be a 400 and an `ArgumentException` escaping a service
becomes a 500.

**Every unsafe request needs an antiforgery token, including sign-in.** The session cookie is `HttpOnly`;
the separate `XSRF-TOKEN` cookie is deliberately readable by script and is echoed in the `X-XSRF-TOKEN`
header. The token is bound to the signed-in identity, so the client discards it after login and logout.

**Access is checked on every token request, not just at sign-in.** The rule (the application is active and
the user holds a direct or group grant) lives in one place and is applied by the authorize endpoint and by
every code exchange and refresh, so taking access away takes effect without waiting for a session to expire.

## Configuration

| Section | Purpose |
| --- | --- |
| `ConnectionStrings:OpenPortal` | database connection string |
| `Database:Provider` | `Sqlite`, or `PostgreSql` once Npgsql is added |
| `Database:MigrateOnStartup` | apply migrations during startup; development only |
| `Identity:Password` | complexity policy, published to the client so forms can match it |
| `Identity:Lockout` | failed-attempt thresholds |
| `Identity:SignIn` | cookie name, lifetime, whether confirmation is required |
| `Identity:Cookie:RequireSecure` | `false` only for the plain-http localhost dev server |
| `Localization` | default language and the languages offered (add one with a line here and a `Messages.<lang>.resx`) |
| `BootstrapAdmin` | one-time first administrator |
| `Access:ProvisioningKey` | shared key applications present when they announce themselves (user secrets); blank disables announcements |
| `Oidc` | token signing/encryption certificates (`SigningCertificatePath`, `EncryptionCertificatePath` + passwords), token lifetimes; required outside Development |
| `DataProtection:KeysPath` | where the key ring lives (default `App_Data/keys`); share it between instances |

## Documentation

- [Wiki](https://github.com/wp-daniel/OpenPortal/wiki) — getting started, single sign-on, localization,
  architecture, testing, publishing, contributing
- [`ARCHITECTURE.md`](ARCHITECTURE.md) — the long form, with the reasoning behind each decision
- [`docs/brand`](docs/brand) — the logo and how it is used

## License

[MIT](LICENSE). Use it, change it, ship it. Third-party licenses are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
