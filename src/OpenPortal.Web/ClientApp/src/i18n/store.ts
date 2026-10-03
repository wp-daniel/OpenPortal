/**
 * The translation store. Framework-free on purpose: `ApiError`, `notify` and the zod schemas all need to
 * translate outside a component, and a React context cannot reach them.
 *
 * Nothing here is bundled per language. The strings come from the server (`GET /api/i18n/messages`), which
 * serves the same `.resx` resources it uses for its own error messages, so a language is added in one place
 * and the client picks it up without a rebuild.
 */

export type Params = Readonly<Record<string, string | number>>
export type TFunction = (key: string, params?: Params) => string

export interface LanguageInfo {
  readonly code: string
  readonly name: string
}

export interface I18nState {
  readonly ready: boolean
  readonly language: string
  readonly languages: readonly LanguageInfo[]
  readonly defaultLanguage: string
  readonly t: TFunction
}

const STORAGE_KEY = 'openportal-lang'

const FALLBACK_LANGUAGES: readonly LanguageInfo[] = [{ code: 'en', name: 'English' }]

const cache = new Map<string, Readonly<Record<string, string>>>()
const listeners = new Set<() => void>()

function safePluralRules(language: string): Intl.PluralRules {
  try {
    return new Intl.PluralRules(language)
  } catch {
    return new Intl.PluralRules('en')
  }
}

function buildState(input: {
  ready: boolean
  language: string
  languages: readonly LanguageInfo[]
  defaultLanguage: string
  messages: Readonly<Record<string, string>>
}): I18nState {
  const rules = safePluralRules(input.language)

  const t: TFunction = (key, params) => {
    let template: string | undefined

    if (params && typeof params.count === 'number') {
      const category = rules.select(params.count)
      template = input.messages[`${key}_${category}`] ?? input.messages[`${key}_other`]
    }

    template ??= input.messages[key]

    if (template === undefined) {
      // A missing key shows the key itself: visibly wrong, greppable, and `npm run i18n:check` fails the
      // build for literal keys long before this reaches a user.
      return key
    }

    return params ? template.replace(/\{(\w+)\}/g, (match, name: string) => `${params[name] ?? match}`) : template
  }

  return {
    ready: input.ready,
    language: input.language,
    languages: input.languages,
    defaultLanguage: input.defaultLanguage,
    t,
  }
}

let state: I18nState = buildState({
  ready: false,
  language: 'en',
  languages: FALLBACK_LANGUAGES,
  defaultLanguage: 'en',
  messages: {},
})

function publish(next: I18nState): void {
  state = next
  document.documentElement.lang = next.language

  for (const listener of listeners) {
    listener()
  }
}

export function subscribe(listener: () => void): () => void {
  listeners.add(listener)

  return () => {
    listeners.delete(listener)
  }
}

export function getState(): I18nState {
  return state
}

/** Translate outside React. Inside components prefer `useI18n().t`, which also re-renders on a change. */
export const translate: TFunction = (key, params) => state.t(key, params)

/** The language to send as `Accept-Language`, so server messages follow the UI language. */
export function currentLanguage(): string {
  return state.language
}

/** Formats a date in the active language (`Intl`, so there is no per-language date table to maintain). */
export function formatDate(value: string | number | Date, options?: Intl.DateTimeFormatOptions): string {
  return new Intl.DateTimeFormat(state.language, options).format(new Date(value))
}

/** Joins items as a natural-language list ("a, b and c") in the active language. */
export function formatList(items: readonly string[]): string {
  return new Intl.ListFormat(state.language, { style: 'long', type: 'conjunction' }).format(items)
}

function readStored(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY)
  } catch {
    return null
  }
}

function writeStored(code: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, code)
  } catch {
    // Not persisting is fine; the choice still applies for this page view.
  }
}

function findLanguage(languages: readonly LanguageInfo[], code: string | null | undefined): string | undefined {
  return code ? languages.find((entry) => entry.code.toLowerCase() === code.toLowerCase())?.code : undefined
}

/** Picks the first offered language among the stored choice, the browser's preferences and the default. */
function pickInitial(languages: readonly LanguageInfo[], defaultLanguage: string): string {
  const stored = findLanguage(languages, readStored())

  if (stored) {
    return stored
  }

  for (const preferred of navigator.languages ?? [navigator.language]) {
    const match = findLanguage(languages, preferred) ?? findLanguage(languages, preferred.split('-')[0])

    if (match) {
      return match
    }
  }

  return findLanguage(languages, defaultLanguage) ?? languages[0]?.code ?? 'en'
}

async function loadMessages(language: string): Promise<Readonly<Record<string, string>>> {
  const cached = cache.get(language)

  if (cached) {
    return cached
  }

  // `no-cache` on the server means the browser revalidates by ETag: a 304 costs a round trip, not the
  // payload, and an edited translation still shows up on the next load.
  const response = await fetch(`/api/i18n/messages?lang=${encodeURIComponent(language)}`, {
    headers: { Accept: 'application/json' },
  })

  if (!response.ok) {
    throw new Error(`Could not load the ${language} messages (${response.status}).`)
  }

  const body = (await response.json()) as { messages: Record<string, string> }
  cache.set(language, body.messages)

  return body.messages
}

async function loadLanguages(): Promise<{ defaultLanguage: string; languages: readonly LanguageInfo[] }> {
  const response = await fetch('/api/i18n/languages', { headers: { Accept: 'application/json' } })

  if (!response.ok) {
    throw new Error(`Could not load the language list (${response.status}).`)
  }

  const body = (await response.json()) as { defaultLanguage: string; languages: LanguageInfo[] }

  return { defaultLanguage: body.defaultLanguage, languages: body.languages }
}

let initialising: Promise<void> | null = null

/** Loads the offered languages and the initial language. Safe to call more than once. */
export function initialise(): Promise<void> {
  initialising ??= (async () => {
    let languages: readonly LanguageInfo[] = FALLBACK_LANGUAGES
    let defaultLanguage = 'en'

    try {
      const loaded = await loadLanguages()
      languages = loaded.languages
      defaultLanguage = loaded.defaultLanguage
    } catch {
      // The server is unreachable. Carry on with English so the shell can still render its error states.
    }

    const language = pickInitial(languages, defaultLanguage)
    let messages: Readonly<Record<string, string>> = {}

    try {
      messages = await loadMessages(language)
    } catch {
      // Same: render with an empty catalog rather than a blank screen.
    }

    publish(buildState({ ready: true, language, languages, defaultLanguage, messages }))
  })()

  return initialising
}

/**
 * Switches the UI language. Resolves once the new strings are in, so the UI changes in one step instead of
 * flashing keys. An unknown code falls back to the default language rather than breaking the page.
 */
export async function setLanguage(code: string): Promise<void> {
  await initialise()

  const { languages, defaultLanguage } = state
  const target = findLanguage(languages, code) ?? findLanguage(languages, defaultLanguage) ?? languages[0]?.code ?? 'en'

  if (target === state.language) {
    return
  }

  const messages = await loadMessages(target)

  writeStored(target)
  publish(buildState({ ready: true, language: target, languages, defaultLanguage, messages }))
}
