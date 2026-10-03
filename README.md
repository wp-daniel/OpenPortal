# OpenPortal

A single-owner site with its own content management: an ASP.NET Core 10 API, a React client, ASP.NET Core
Identity for accounts, and no third-party dependency anywhere in the stack.

An administrator signs in to write a profile and projects; visitors read them. That is the whole product.

## Requirements

| Tool | Version |
| --- | --- |
| .NET SDK | 10.0.400 (pinned in `global.json`) |
| Node.js | 22 or newer |

`dotnet-ef` is restored as a local tool, so there is nothing to install globally:

```bash
dotnet tool restore
```

## Running it

```bash
dotnet run --project src/OpenPortal.Web
```

That single command builds the client, builds the host, applies migrations and starts listening. In
development the Vite dev server is proxied through the ASP.NET origin, so the browser only ever talks to
one host and the session cookie stays first-party.

Open the URL Kestrel prints. There is no content yet — create the first administrator:

```bash
dotnet user-secrets --project src/OpenPortal.Web set "BootstrapAdmin:Enabled" "true"
dotnet user-secrets --project src/OpenPortal.Web set "BootstrapAdmin:Email" "you@example.com"
dotnet user-secrets --project src/OpenPortal.Web set "BootstrapAdmin:Password" "<a strong password>"
```

Restart, sign in at `/sign-in`, then edit your profile under **Profile** and add work under **Manage
projects**. Turn the bootstrap back off once the account exists; it only creates a missing one, so it is
harmless but should not stay enabled in a deployment.

Credentials belong in user secrets, never in a committed `appsettings*.json`.

### Useful variations

```bash
# Backend only, without building the client.
dotnet run --project src/OpenPortal.Web -p:SkipClientBuild=true

# Work on the client alone with fast refresh.
cd src/OpenPortal.Web/ClientApp && npm run dev

# Type-check the client without emitting.
cd src/OpenPortal.Web/ClientApp && npm run build
```

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

- set `Database:MigrateOnStartup` to `false` and apply migrations as a separate ordered step
  (`dotnet ef database update`), because several instances migrating at once is a race;
- keep `Identity:Cookie:RequireSecure` at `true` and terminate TLS in front of the app;
- supply `ConnectionStrings:OpenPortal` and, if you move off SQLite, add the Npgsql package and wire
  `UseNpgsql` in `DatabaseProviderSelector`.

## Layout

| Path | Contents |
| --- | --- |
| `src/OpenPortal.SharedKernel` | `Result`, `Error`, `IClock`, `TextRules` |
| `src/Modules/Identity` | accounts, sessions, roles — Domain / Application / Infrastructure |
| `src/Modules/Content` | profile and projects — Domain / Application / Infrastructure |
| `src/OpenPortal.Web` | composition root, controllers, middleware, migrations, the client |
| `tests` | unit, architecture and integration suites |

`ARCHITECTURE.md` explains why it is arranged this way, what each boundary buys, and which test enforces it.

## How it is put together

Three things are worth knowing before changing anything.

**Operations return `Result`, they do not throw.** A failure carries a stable error code that reaches the
client as the `errorCode` extension of a problem+json response. Domain entities validate before constructing
rather than throwing, because a rejected input must be a 400 and an `ArgumentException` escaping a service
becomes a 500.

**Every unsafe request needs an antiforgery token, including sign-in.** The session cookie is `HttpOnly`;
the separate `XSRF-TOKEN` cookie is deliberately readable by script and is echoed in the `X-XSRF-TOKEN`
header. The token is bound to the signed-in identity, so the client discards it after login and logout.

**Public and management payloads are different types.** `ProjectDto` is what anonymous readers get — slug
addressed, no ids, no draft flags. `ManagedProjectDto` is what the editor gets. Merging them would either
expose draft work or break the editor.

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
| `BootstrapAdmin` | one-time first administrator |

The password policy is served through `/api/auth/session` rather than duplicated in the client, so a form
can never disagree with what the server enforces.

## License

[MIT](LICENSE). Use it, change it, ship it. Third-party licenses are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
