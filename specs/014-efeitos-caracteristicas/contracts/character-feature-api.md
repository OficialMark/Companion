# Contract delta: Character features

Os endpoints existentes permanecem:

- `POST /api/campaigns/{campaignId}/participants/me/character/features`
- `PUT /api/campaigns/{campaignId}/participants/me/character/features/{featureId}`
- demais comandos de usos, restauração, movimento e exclusão sem mudança de rota

## CharacterFeatureData

```json
{
  "name": "Dueling",
  "source": "Paladino 2",
  "description": "+2 no dano com uma arma corpo a corpo adequada.",
  "maxUses": null,
  "currentUses": 0,
  "recovery": "None",
  "effects": [
    { "type": "DamageBonus", "target": "melee", "op": "Add", "value": 2 }
  ]
}
```

Compatibilidade: `effects` ausente ou `null` em dados persistidos é lido como `[]`; respostas novas sempre retornam uma lista.

Todas as escritas continuam respondendo com o `CharacterDto` completo, incluindo `stats` recalculado.

