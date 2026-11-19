<script setup lang="ts">
import type { AbilityCode, ArmorType, CharacterSheetData, SkillTraining } from '~/types/character'
import { ABILITY_LABEL, ABILITY_NAME, ABILITY_ORDER, SKILLS, abilityKey } from '~/types/character'

/**
 * Main tab of the player's sheet (spec 011, US1). Editable base fields write to the store's
 * draft; every derived number is read from `stats`, which only the server calculates.
 */
const character = useCharacterStore()
const draft = computed(() => character.draft!)
const stats = computed(() => character.stats!)
const stale = computed(() => character.stale)

const SCORE_FIELD: Record<AbilityCode, keyof CharacterSheetData> = {
  Str: 'strength', Dex: 'dexterity', Con: 'constitution', Int: 'intelligence', Wis: 'wisdom', Cha: 'charisma'
}
const TRAINING_NEXT: Record<SkillTraining, SkillTraining> = { None: 'Proficient', Proficient: 'Expertise', Expertise: 'None' }
const TRAINING_LABEL: Record<SkillTraining, string> = { None: 'Sem proficiência', Proficient: 'Proficiente', Expertise: 'Especialista' }
const ARMOR_TYPES: { value: ArmorType; label: string }[] = [
  { value: 'Light', label: 'Leves' }, { value: 'Medium', label: 'Médias' }, { value: 'Heavy', label: 'Pesadas' }
]

function edit(patch: Partial<CharacterSheetData>) { character.editSheet(patch) }
function text(field: keyof CharacterSheetData, event: Event) {
  edit({ [field]: (event.target as HTMLInputElement).value || null } as Partial<CharacterSheetData>)
}
function number(field: keyof CharacterSheetData, event: Event, min: number, max: number) {
  const raw = (event.target as HTMLInputElement).valueAsNumber
  if (Number.isNaN(raw)) return
  edit({ [field]: Math.min(max, Math.max(min, Math.trunc(raw))) } as Partial<CharacterSheetData>)
}
function decimal(field: 'baseSpeed', event: Event) {
  const raw = (event.target as HTMLInputElement).valueAsNumber
  if (Number.isNaN(raw)) return
  edit({ [field]: Math.min(300, Math.max(0, Math.round(raw * 10) / 10)) })
}
function toggleSave(code: AbilityCode) {
  const current = draft.value.savingThrowProficiencies
  edit({ savingThrowProficiencies: current.includes(code) ? current.filter(x => x !== code) : [...current, code] })
}
function cycleSkill(key: string) {
  const current = draft.value.skills[key] ?? 'None'
  const next = TRAINING_NEXT[current]
  const skills = { ...draft.value.skills }
  if (next === 'None') delete skills[key]
  else skills[key] = next
  edit({ skills })
}
function toggleArmor(type: ArmorType) {
  const general = draft.value.generalProficiencies
  const armor = general.armor.includes(type) ? general.armor.filter(x => x !== type) : [...general.armor, type]
  edit({ generalProficiencies: { ...general, armor } })
}
function setGeneral(patch: Partial<CharacterSheetData['generalProficiencies']>) {
  edit({ generalProficiencies: { ...draft.value.generalProficiencies, ...patch } })
}
function setDarkvision(event: Event) {
  const raw = (event.target as HTMLInputElement).valueAsNumber
  const senses = { ...draft.value.baseSenses }
  if (Number.isNaN(raw) || raw <= 0) delete senses.darkvision
  else senses.darkvision = Math.min(300, raw)
  edit({ baseSenses: senses })
}
function signed(value: number) { return value >= 0 ? `+${value}` : String(value) }

const speedText = computed(() => {
  const walk = stats.value.speed.walk?.value ?? 0
  return `${walk.toLocaleString('pt-BR')} m`
})
const otherSpeeds = computed(() => Object.entries(stats.value.speed).filter(([mode]) => mode !== 'walk'))
const SPEED_LABEL: Record<string, string> = { fly: 'Voo', swim: 'Natação', climb: 'Escalada' }
const SENSE_LABEL: Record<string, string> = { darkvision: 'Visão no escuro', blindsight: 'Percepção às cegas', tremorsense: 'Sentido sísmico', truesight: 'Visão verdadeira' }
</script>

<template>
  <div v-if="character.draft && character.stats" class="sheet-core">
    <section class="sheet-section sheet-identity" aria-labelledby="sheet-identity-title">
      <h2 id="sheet-identity-title" class="sheet-section-title">Personagem</h2>
      <div class="sheet-grid identity-grid">
        <label class="sheet-field wide"><span>Nome do personagem</span>
          <input data-field="characterName" maxlength="60" :value="draft.characterName ?? ''" @input="text('characterName', $event)">
        </label>
        <label class="sheet-field"><span>Classe</span>
          <input data-field="className" maxlength="60" :value="draft.className ?? ''" @input="text('className', $event)">
        </label>
        <label class="sheet-field"><span>Subclasse</span>
          <input data-field="subclass" maxlength="60" :value="draft.subclass ?? ''" @input="text('subclass', $event)">
        </label>
        <label class="sheet-field short"><span>Nível</span>
          <input data-field="level" type="number" inputmode="numeric" min="1" max="20" :value="draft.level" @input="number('level', $event, 1, 20)">
        </label>
        <label class="sheet-field"><span>Espécie</span>
          <input data-field="species" maxlength="60" :value="draft.species ?? ''" @input="text('species', $event)">
        </label>
        <label class="sheet-field"><span>Antecedente</span>
          <input data-field="background" maxlength="60" :value="draft.background ?? ''" @input="text('background', $event)">
        </label>
        <label class="sheet-field"><span>Alinhamento</span>
          <input data-field="alignment" maxlength="60" :value="draft.alignment ?? ''" @input="text('alignment', $event)">
        </label>
        <label class="sheet-field short"><span>Experiência</span>
          <input data-field="experience" type="number" inputmode="numeric" min="0" max="999999" :value="draft.experience" @input="number('experience', $event, 0, 999999)">
        </label>
      </div>
    </section>

    <section class="sheet-section" aria-labelledby="sheet-combat-title">
      <h2 id="sheet-combat-title" class="sheet-section-title">Combate</h2>
      <div class="sheet-combat-strip">
        <DerivedStat name="armorClass" label="CA" :value="stats.armorClass.value" :parts="stats.armorClass.parts" :stale="stale" />
        <DerivedStat name="initiative" label="Iniciativa" :value="stats.initiative.value" :parts="stats.initiative.parts" :stale="stale" signed />
        <DerivedStat name="speed" label="Deslocamento" :value="speedText" :parts="stats.speed.walk?.parts ?? []" :stale="stale" />
        <DerivedStat name="proficiencyBonus" label="Proficiência" :value="stats.proficiencyBonus" :parts="[{ label: 'prof', value: stats.proficiencyBonus, source: `Nível ${draft.level}`, itemId: null }]" :stale="stale" signed />
        <DerivedStat name="passivePerception" label="Percepção passiva" :value="stats.passivePerception.value" :parts="stats.passivePerception.parts" :stale="stale" />
      </div>
      <div class="sheet-grid hp-grid">
        <label class="sheet-field short"><span>PV atuais</span>
          <input data-field="currentHitPoints" type="number" inputmode="numeric" min="0" :max="stats.maxHitPoints.value" :value="draft.currentHitPoints" @input="number('currentHitPoints', $event, 0, 99999)">
        </label>
        <DerivedStat name="maxHitPoints" label="PV máximos" :value="stats.maxHitPoints.value" :parts="stats.maxHitPoints.parts" :stale="stale" />
        <label class="sheet-field short"><span>PV temporários</span>
          <input data-field="temporaryHitPoints" type="number" inputmode="numeric" min="0" max="9999" :value="draft.temporaryHitPoints" @input="number('temporaryHitPoints', $event, 0, 9999)">
        </label>
      </div>
      <p v-if="otherSpeeds.length || Object.keys(stats.senses).length" class="sheet-inline-list" data-derived="movement-senses">
        <span v-for="[mode, speed] in otherSpeeds" :key="mode">{{ SPEED_LABEL[mode] ?? mode }} {{ speed.value.toLocaleString('pt-BR') }} m</span>
        <span v-for="(sense, key) in stats.senses" :key="key">{{ SENSE_LABEL[key] ?? key }} {{ sense.value.toLocaleString('pt-BR') }} m</span>
      </p>
    </section>

    <section class="sheet-section" aria-labelledby="sheet-abilities-title">
      <h2 id="sheet-abilities-title" class="sheet-section-title">Atributos e salvaguardas</h2>
      <div class="ability-grid">
        <article v-for="code in ABILITY_ORDER" :key="code" class="ability-card" :data-ability="abilityKey(code)">
          <header>
            <span class="ability-abbr">{{ ABILITY_LABEL[code] }}</span>
            <span class="ability-name">{{ ABILITY_NAME[code] }}</span>
          </header>
          <DerivedStat
            :name="`ability-${abilityKey(code)}`"
            label="Modificador"
            :value="stats.abilities[abilityKey(code)].modifier"
            :parts="stats.abilities[abilityKey(code)].parts"
            :stale="stale"
            signed
          />
          <label class="ability-base"><span>Base</span>
            <input :data-field="SCORE_FIELD[code]" type="number" inputmode="numeric" min="1" max="30" :value="draft[SCORE_FIELD[code]] as number" @input="number(SCORE_FIELD[code], $event, 1, 30)">
          </label>
          <p class="ability-final" :data-derived="`final-${abilityKey(code)}`">
            Final <strong>{{ stats.abilities[abilityKey(code)].final }}</strong>
          </p>
          <button
            type="button"
            class="save-toggle"
            :aria-pressed="draft.savingThrowProficiencies.includes(code)"
            :data-save="abilityKey(code)"
            @click="toggleSave(code)"
          >
            <Icon :name="draft.savingThrowProficiencies.includes(code) ? 'lucide:circle-check' : 'lucide:circle'" />
            <span>Salvaguarda</span>
            <strong :data-derived="`save-${abilityKey(code)}`">{{ signed(stats.savingThrows[abilityKey(code)].bonus) }}</strong>
          </button>
        </article>
      </div>
    </section>

    <section class="sheet-section" aria-labelledby="sheet-skills-title">
      <h2 id="sheet-skills-title" class="sheet-section-title">Perícias</h2>
      <ul class="skill-list">
        <li v-for="skill in SKILLS" :key="skill.key" class="skill-row" :data-skill="skill.key">
          <button
            type="button"
            class="skill-training"
            :data-training="draft.skills[skill.key] ?? 'None'"
            :aria-label="`${skill.name}: ${TRAINING_LABEL[draft.skills[skill.key] ?? 'None']}. Mudar`"
            @click="cycleSkill(skill.key)"
          >
            <Icon :name="(draft.skills[skill.key] ?? 'None') === 'Expertise' ? 'lucide:star' : (draft.skills[skill.key] ?? 'None') === 'Proficient' ? 'lucide:circle-check' : 'lucide:circle'" />
          </button>
          <span class="skill-name">{{ skill.name }} <small>({{ ABILITY_LABEL[skill.ability] }})</small></span>
          <strong class="skill-bonus" :data-derived="`skill-${skill.key}`" :title="stats.skills[skill.key].parts.map(p => `${p.source} ${signed(p.value)}`).join(' · ')">
            {{ signed(stats.skills[skill.key].bonus) }}
          </strong>
        </li>
      </ul>
    </section>

    <section class="sheet-section" aria-labelledby="sheet-base-title">
      <h2 id="sheet-base-title" class="sheet-section-title">Valores-base</h2>
      <p class="sheet-hint">Itens equipados somam por cima destes valores, sem alterá-los.</p>
      <div class="sheet-grid">
        <label class="sheet-field short"><span>PV máximos base</span>
          <input data-field="baseMaxHitPoints" type="number" inputmode="numeric" min="1" max="9999" :value="draft.baseMaxHitPoints" @input="number('baseMaxHitPoints', $event, 1, 9999)">
        </label>
        <label class="sheet-field short"><span>CA sem armadura</span>
          <input data-field="unarmoredBaseArmorClass" type="number" inputmode="numeric" min="0" max="30" :value="draft.unarmoredBaseArmorClass" @input="number('unarmoredBaseArmorClass', $event, 0, 30)">
        </label>
        <label class="sheet-field"><span>Soma sem armadura</span>
          <select data-field="unarmoredAbility" :value="draft.unarmoredAbility ?? ''" @change="edit({ unarmoredAbility: (($event.target as HTMLSelectElement).value || null) as AbilityCode | null })">
            <option value="">Só DES</option>
            <option v-for="code in ABILITY_ORDER.filter(x => x !== 'Dex')" :key="code" :value="code">DES + {{ ABILITY_LABEL[code] }}</option>
          </select>
        </label>
        <label class="sheet-field short"><span>Deslocamento (m)</span>
          <input data-field="baseSpeed" type="number" inputmode="decimal" min="0" max="300" step="1.5" :value="draft.baseSpeed" @input="decimal('baseSpeed', $event)">
        </label>
        <label class="sheet-field short"><span>Visão no escuro (m)</span>
          <input data-field="darkvision" type="number" inputmode="decimal" min="0" max="300" step="1.5" :value="draft.baseSenses.darkvision ?? ''" @input="setDarkvision">
        </label>
      </div>
    </section>

    <section class="sheet-section" aria-labelledby="sheet-casting-title">
      <h2 id="sheet-casting-title" class="sheet-section-title">Conjuração</h2>
      <div class="sheet-grid casting-grid">
        <label class="sheet-field"><span>Atributo de conjuração</span>
          <select data-field="spellcastingAbility" :value="draft.spellcastingAbility ?? ''" @change="edit({ spellcastingAbility: (($event.target as HTMLSelectElement).value || null) as AbilityCode | null })">
            <option value="">Nenhum</option>
            <option v-for="code in ABILITY_ORDER" :key="code" :value="code">{{ ABILITY_NAME[code] }}</option>
          </select>
        </label>
        <DerivedStat v-if="stats.spellSaveDc" name="spellSaveDc" label="CD de magia" :value="stats.spellSaveDc.value" :parts="stats.spellSaveDc.parts" :stale="stale" />
        <DerivedStat v-if="stats.spellAttackBonus" name="spellAttackBonus" label="Ataque mágico" :value="stats.spellAttackBonus.value" :parts="stats.spellAttackBonus.parts" :stale="stale" signed />
      </div>
    </section>

    <section class="sheet-section" aria-labelledby="sheet-prof-title">
      <h2 id="sheet-prof-title" class="sheet-section-title">Proficiências gerais</h2>
      <p class="sheet-hint">Servem de valor inicial para cada arma e armadura que você adicionar. Depois, vale a marcação do próprio item.</p>
      <div class="chip-row" role="group" aria-label="Armaduras">
        <button v-for="type in ARMOR_TYPES" :key="type.value" type="button" class="chip" :aria-pressed="draft.generalProficiencies.armor.includes(type.value)" :data-armor-prof="type.value" @click="toggleArmor(type.value)">
          Armaduras {{ type.label.toLowerCase() }}
        </button>
        <button type="button" class="chip" :aria-pressed="draft.generalProficiencies.shields" data-shield-prof @click="setGeneral({ shields: !draft.generalProficiencies.shields })">Escudos</button>
        <button type="button" class="chip" :aria-pressed="draft.generalProficiencies.simpleWeapons" data-simple-prof @click="setGeneral({ simpleWeapons: !draft.generalProficiencies.simpleWeapons })">Armas simples</button>
        <button type="button" class="chip" :aria-pressed="draft.generalProficiencies.martialWeapons" data-martial-prof @click="setGeneral({ martialWeapons: !draft.generalProficiencies.martialWeapons })">Armas marciais</button>
      </div>
      <label class="sheet-field wide"><span>Ferramentas, idiomas e outras</span>
        <input data-field="otherProficiencies" maxlength="500" :value="draft.generalProficiencies.other ?? ''" @input="setGeneral({ other: ($event.target as HTMLInputElement).value || null })">
      </label>
    </section>

    <section v-if="stats.warnings.length || stats.suppressedEffects.length || stats.resistances.length || stats.immunities.length || stats.conditions.length || stats.textEffects.length" class="sheet-section" aria-labelledby="sheet-effects-title" data-active-effects>
      <h2 id="sheet-effects-title" class="sheet-section-title">Efeitos e avisos</h2>
      <ul class="effect-notes">
        <li v-for="warning in stats.warnings" :key="warning" class="warning"><Icon name="lucide:triangle-alert" />{{ warning }}</li>
        <li v-for="item in stats.suppressedEffects" :key="`${item.itemId}-${item.effectKey}`" data-suppressed-note>
          <Icon name="lucide:copy-x" />Efeito de {{ item.itemName }} ignorado: igual ao de {{ item.keptItemName }}, que já conta.
        </li>
        <li v-if="stats.resistances.length"><Icon name="lucide:shield-half" />Resistência: {{ stats.resistances.join(', ') }}</li>
        <li v-if="stats.immunities.length"><Icon name="lucide:shield-check" />Imunidade: {{ stats.immunities.join(', ') }}</li>
        <li v-if="stats.conditions.length"><Icon name="lucide:activity" />Condições: {{ stats.conditions.join(', ') }}</li>
        <li v-for="note in stats.textEffects" :key="note"><Icon name="lucide:sparkles" />{{ note }}</li>
      </ul>
    </section>

    <section class="sheet-section" aria-labelledby="sheet-notes-title">
      <h2 id="sheet-notes-title" class="sheet-section-title">Traços e anotações</h2>
      <label class="sheet-field wide"><span>Traços</span>
        <textarea data-field="traits" maxlength="4000" rows="4" :value="draft.traits ?? ''" @input="text('traits', $event)" />
      </label>
      <label class="sheet-field wide"><span>Anotações</span>
        <textarea data-field="notes" maxlength="4000" rows="4" :value="draft.notes ?? ''" @input="text('notes', $event)" />
      </label>
    </section>
  </div>
</template>
