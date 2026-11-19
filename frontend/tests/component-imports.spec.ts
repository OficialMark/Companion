import { readdirSync, readFileSync } from 'node:fs'
import { basename, join } from 'node:path'

// Spec 011 regression: Nuxt registers app/components/character/X.vue as "CharacterX". A bare <X>
// tag does not resolve and renders nothing, silently, in the real app (unit tests mount with stubs
// and never notice). Every use of a character component must import it explicitly.
const appDir = join(__dirname, '..', 'app')
const characterDir = join(appDir, 'components', 'character')
const components = readdirSync(characterDir).filter(file => file.endsWith('.vue')).map(file => basename(file, '.vue'))

function vueFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
    const path = join(dir, entry.name)
    if (entry.isDirectory()) return vueFiles(path)
    return entry.name.endsWith('.vue') ? [path] : []
  })
}

describe('componentes da ficha usados com import explícito', () => {
  const offenders = vueFiles(appDir).flatMap(file => {
    const source = readFileSync(file, 'utf8')
    return components
      .filter(name => new RegExp(`<${name}[\\s/>]`).test(source) && !new RegExp(`import ${name}\\b`).test(source))
      .map(name => `${file.replace(appDir, 'app')} usa <${name}> sem import`)
  })

  it('nenhuma tag de componente da ficha fica sem import', () => {
    expect(offenders).toEqual([])
  })
})
