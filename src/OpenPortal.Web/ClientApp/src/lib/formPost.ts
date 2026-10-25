import { antiforgeryToken } from '@/api/client'

/**
 * Submits a classic form post (a full page navigation) with the antiforgery token as a form field, for the endpoints
 * that answer with a redirect the browser must follow: signing in with an external provider, ending a session for
 * another application. A fetch would follow the redirect itself and leave the page where it was.
 */
export async function postForm(action: string, fields: Record<string, string> | URLSearchParams = {}): Promise<void> {
  const form = document.createElement('form')
  form.method = 'POST'
  form.action = action

  const values = new URLSearchParams(fields instanceof URLSearchParams ? fields : Object.entries(fields))
  values.set('__RequestVerificationToken', await antiforgeryToken())

  for (const [name, value] of values) {
    const input = document.createElement('input')
    input.type = 'hidden'
    input.name = name
    input.value = value
    form.appendChild(input)
  }

  document.body.appendChild(form)
  form.submit()
}
