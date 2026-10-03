// Verifies the translation catalog, because the compiler cannot: keys are plain strings and the strings
// live in the server's .resx files.
//
//   1. every literal key used in src/ (t('...'), translate('...'), labelKey: '...') exists in the neutral
//      Messages.resx (a plural key may exist as key_one/key_other instead);
//   2. every Messages.<lang>.resx defines exactly the keys of the neutral file, so adding a language cannot
//      silently leave strings untranslated.
//
// Unused keys are only reported, since some keys are built dynamically (role.<name>) or used by the server.

import { readdirSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const resources = join(root, '..', 'Resources')

function keysOf(file) {
  const xml = readFileSync(join(resources, file), 'utf8')

  return new Set([...xml.matchAll(/<data name="([^"]+)"/g)].map((match) => match[1]))
}

function* walk(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name)

    if (entry.isDirectory()) {
      yield* walk(path)
    } else if (/\.(ts|tsx)$/.test(entry.name)) {
      yield path
    }
  }
}

const neutral = keysOf('Messages.resx')
const problems = []
const used = new Set()

for (const file of walk(join(root, 'src'))) {
  const source = readFileSync(file, 'utf8')
  const pattern = /(?:\b(?:t|translate)\(\s*|labelKey:\s*)'([A-Za-z][\w.]*)'/g

  for (const match of source.matchAll(pattern)) {
    const key = match[1]
    used.add(key)

    if (!neutral.has(key) && !neutral.has(`${key}_other`)) {
      problems.push(`${file.slice(root.length + 1)}: unknown translation key '${key}'`)
    }
  }
}

for (const file of readdirSync(resources).filter((name) => /^Messages\.[\w-]+\.resx$/.test(name))) {
  const keys = keysOf(file)

  for (const key of neutral) {
    if (!keys.has(key)) {
      problems.push(`${file}: missing key '${key}'`)
    }
  }

  for (const key of keys) {
    if (!neutral.has(key)) {
      problems.push(`${file}: key '${key}' is not in Messages.resx`)
    }
  }
}

const serverPrefixes = ['error.', 'problem.', 'role.']
const base = (key) => key.replace(/_(zero|one|two|few|many|other)$/, '')
const unused = [...neutral].filter(
  (key) => !used.has(base(key)) && !serverPrefixes.some((prefix) => key.startsWith(prefix)) && !/^validation\.\w+\.\w+$/.test(key),
)

if (unused.length > 0) {
  console.warn(`i18n: ${unused.length} key(s) not referenced by the client: ${unused.join(', ')}`)
}

if (problems.length > 0) {
  console.error(problems.join('\n'))
  process.exit(1)
}

console.log(`i18n: ${neutral.size} keys, all languages in sync.`)
