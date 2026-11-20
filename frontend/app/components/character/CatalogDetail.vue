<script setup lang="ts">
import type { CatalogItem, CatalogSpell } from '~/types/character'
import { CATEGORY_LABEL } from '~/types/character'

/** Read-only details of a catalog item or spell (spec 012, Compêndio > Itens / Magias). */
const props = defineProps<{ item?: CatalogItem | null; spell?: CatalogSpell | null }>()

function levelLabel(value: number) { return value === 0 ? 'Truque' : `${value}º círculo` }
function meters(value: number | null) { return value == null ? null : `${String(value).replace('.', ',')} m` }

const facts = computed<Array<[string, string]>>(() => {
  const rows: Array<[string, string | null | undefined]> = []
  const item = props.item
  if (item) {
    const t = item.template
    rows.push(['Categoria', item.subcategory || CATEGORY_LABEL[item.category]])
    rows.push(['Raridade', item.rarity])
    if (item.requiresAttunement) rows.push(['Sintonia', 'Requer sintonia'])
    if (t.magicBonus > 0) rows.push(['Bônus mágico', `+${t.magicBonus}`])
    if (t.weapon) {
      rows.push(['Dano', [t.weapon.damageDice, t.weapon.damageType].filter(Boolean).join(' ')])
      if (t.weapon.versatileDice) rows.push(['Versátil', t.weapon.versatileDice])
      if (t.weapon.rangeNormal) rows.push(['Alcance', [meters(t.weapon.rangeNormal), meters(t.weapon.rangeLong)].filter(Boolean).join(' / ')])
      if (t.weapon.properties.length) rows.push(['Propriedades', t.weapon.properties.join(', ')])
      rows.push(['Maestria', t.weapon.mastery])
    }
    if (t.armor) {
      rows.push(['CA base', String(t.armor.baseAc)])
      if (t.armor.strengthMin) rows.push(['Força mínima', String(t.armor.strengthMin)])
      if (t.armor.stealthDisadvantage) rows.push(['Furtividade', 'Desvantagem'])
    }
    if (t.shield) rows.push(['CA', `+${t.shield.acBonus}`])
    rows.push(['Peso', t.weight == null ? null : `${String(t.weight).replace('.', ',')} kg`])
    rows.push(['Custo', item.cost])
    if (item.requiresBaseItem) rows.push(['Base', 'Escolhida ao adicionar à ficha'])
  }
  const spell = props.spell
  if (spell) {
    const t = spell.template
    rows.push(['Círculo', `${levelLabel(spell.level)}${t.school ? `, ${t.school}` : ''}${t.ritual ? ' (ritual)' : ''}`])
    rows.push(['Tempo de conjuração', t.castingTime])
    rows.push(['Alcance', t.range])
    rows.push(['Componentes', t.components])
    rows.push(['Duração', `${t.concentration ? 'Concentração, ' : ''}${t.duration ?? ''}`.replace(/, $/, '') || null])
    if (spell.classes.length) rows.push(['Classes', spell.classes.join(', ')])
  }
  return rows.filter((row): row is [string, string] => Boolean(row[1]))
})
const description = computed(() => props.item?.template.description ?? props.spell?.template.description ?? null)
// "Em Níveis Superiores" is already part of the description (CatalogSeeder): shown once.
const higherLevels = computed(() => {
  const text = props.spell?.higherLevels
  return text && !(description.value ?? '').includes(text.trim()) ? text : null
})
</script>

<template>
  <div class="catalog-detail" data-catalog-detail>
    <dl v-if="facts.length" class="catalog-detail-facts">
      <div v-for="[label, value] in facts" :key="label"><dt>{{ label }}</dt><dd>{{ value }}</dd></div>
    </dl>
    <p v-if="description" class="catalog-detail-text">{{ description }}</p>
    <p v-if="higherLevels" class="catalog-detail-text"><strong>Em círculos superiores.</strong> {{ higherLevels }}</p>
  </div>
</template>
