/**
 * The closed list of icons a saved roll can use (spec 011, FR-033), in the same order as
 * `SavedRollIcons.All` in backend/src/Companion.Domain/Characters/SavedRollData.cs.
 * tests/saved-roll-icons.spec.ts reads the C# file and fails if the two lists drift apart.
 */
export const SAVED_ROLL_ICONS = [
  'lucide:sword', 'lucide:swords', 'lucide:axe', 'lucide:crosshair', 'lucide:shield',
  'lucide:wand-sparkles', 'lucide:sparkles', 'lucide:flame', 'lucide:snowflake', 'lucide:zap',
  'lucide:skull', 'lucide:heart-pulse', 'lucide:footprints', 'lucide:eye', 'lucide:brain', 'lucide:dices'
] as const

export type SavedRollIcon = typeof SAVED_ROLL_ICONS[number]

/** Accessible names (PT-BR) for the icon picker. */
export const SAVED_ROLL_ICON_LABEL: Record<SavedRollIcon, string> = {
  'lucide:sword': 'Espada',
  'lucide:swords': 'Espadas cruzadas',
  'lucide:axe': 'Machado',
  'lucide:crosshair': 'Mira',
  'lucide:shield': 'Escudo',
  'lucide:wand-sparkles': 'Varinha',
  'lucide:sparkles': 'Brilho',
  'lucide:flame': 'Chama',
  'lucide:snowflake': 'Floco de neve',
  'lucide:zap': 'Raio',
  'lucide:skull': 'Caveira',
  'lucide:heart-pulse': 'Coração',
  'lucide:footprints': 'Pegadas',
  'lucide:eye': 'Olho',
  'lucide:brain': 'Cérebro',
  'lucide:dices': 'Dados'
}

export const DEFAULT_SAVED_ROLL_ICON: SavedRollIcon = 'lucide:dices'

export function isSavedRollIcon(icon: string): icon is SavedRollIcon {
  return (SAVED_ROLL_ICONS as readonly string[]).includes(icon)
}
