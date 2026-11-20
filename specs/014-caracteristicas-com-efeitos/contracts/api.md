# API: spec 014

Nenhuma rota nova. Muda o corpo das rotas de característica da spec 013:

```text
CharacterFeatureData { name, source?, description?, maxUses?, currentUses, recovery, effects: ItemEffect[] }
ItemEffect { type, target, op, value }   # target de AttackBonus/DamageBonus aceita "melee-one-handed"
```

`effects` ausente no corpo = lista vazia. As respostas (`CharacterDto.stats`) passam a incluir as partes vindas das características, com `source` = nome da característica e `itemId` = id da característica.
