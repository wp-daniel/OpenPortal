# The application API

An application that signs its users in through OpenPortal can also talk to the portal **from its own backend**,
without a user and without the portal's screens: list the people who can open it, grant or withdraw their
access (with the application's roles), and add new colleagues.

The reference, with every endpoint, schema and a "try it" client, is at **`/scalar`** on the portal (the
document behind it is `/openapi/apps.json`). It shows only what applications use: OpenID Connect sign-in,
the application API and the announcement endpoint. The portal's own administration API is not in it.

## 1. Enable it for the application

Nothing is allowed by default. In **Administration → Applications**, edit the application and open the **API**
tab:

| Scope | Allows |
| --- | --- |
| `portal.users.read` | `GET /api/apps/me/users`, `GET /api/apps/me/users/{id}` |
| `portal.access.manage` | `PUT` / `DELETE /api/apps/me/users/{id}/access`, `POST /api/apps/me/users` for an existing account |
| `portal.users.create` | `POST /api/apps/me/users` for a new account (ordinary, never an administrator). Needs `portal.access.manage`. |

Changing the scopes or disabling the application takes effect on the next request, even for tokens already
issued: every call re-reads the application.

## 2. Get a token

The application's client id and secret (shown once at approval, or regenerated in the portal):

```bash
curl -s https://portal.example.com/connect/token \
  -d grant_type=client_credentials \
  -d client_id=crm \
  -d client_secret="$OPENPORTAL_CLIENT_SECRET"
```

Without `scope` the token carries every enabled scope; ask for fewer with `-d "scope=portal.users.read"`. The
answer is an `access_token` valid for a few minutes (`expires_in`); ask for a new one when it expires. There is
no refresh token and no user in it.

## 3. Call the API

```bash
curl -s https://portal.example.com/api/apps/me/users?search=rossi \
  -H "Authorization: Bearer $TOKEN"
```

| Method | Path | What it does |
| --- | --- | --- |
| `GET` | `/api/apps/me` | The application: roles, groups claim setting, enabled scopes. |
| `GET` | `/api/apps/me/users?search=&page=&pageSize=` | Users who can open the application (directly or through a group), with their roles and groups. |
| `GET` | `/api/apps/me/users/{id}` | One of them; 404 for anyone without access. |
| `POST` | `/api/apps/me/users` | Add a person by `email`: an existing account is granted the application; a new one also needs `password`, `firstName`, `lastName`. Optional `roles`. 201 when an account was created. |
| `PUT` | `/api/apps/me/users/{id}/access` | Grant the application to the user personally. `{ "roles": ["sales"] }` sets exactly those roles; no body keeps the existing ones. |
| `DELETE` | `/api/apps/me/users/{id}/access` | Withdraw the personal grant and end the user's sessions in the application. `stillHasAccess` is true when a group still grants it (groups are managed in the portal). |

Errors are `application/problem+json` with an `errorCode`, like the rest of the portal: `access.api_scope_missing`
(403, the token lacks the scope or it was switched off), `access.api_caller_unknown` (403, not an active
application's own token), `access.role_not_found` (400), `identity.duplicate_email` (409), password policy
errors (400).

Every change is in the portal's audit log with the application as the actor. A user's own access token, or a
portal session, never opens these endpoints.
