/**
 * Where to send the browser after sign-in when another application started the sign-in.
 *
 * The OpenID Connect authorization endpoint redirects an anonymous user to `/sign-in?returnUrl=...` and
 * expects to be called again afterwards. That endpoint is served by the server, not by this router, so the
 * return has to be a full page load. Only a local path to the authorization endpoint is accepted: anything
 * else (another origin, `//host`, a backslash trick) is ignored, so the parameter cannot be used as an open
 * redirect.
 */
export function serverReturnUrl(search: string): string | null {
  const value = new URLSearchParams(search).get('returnUrl')

  if (!value || !value.startsWith('/connect/authorize') || value.includes('\\')) {
    return null
  }

  // Resolve against this origin and make sure it stayed here.
  const resolved = new URL(value, window.location.origin)

  return resolved.origin === window.location.origin ? `${resolved.pathname}${resolved.search}` : null
}
