# Contract: Typed Character Damage

All existing character/catalog routes and authorization remain in place. The API continues to accept legacy `damageType` on weapon payloads and legacy saved rolls without `damageComponents`.

## Weapon payload extension

`weapon.damageTypes` is an optional array of 1–8 strings, each normalized to a known damage key or bounded custom label. `weapon.damageType` remains the legacy primary string. On read, `damageTypes` is populated from the list, or from `damageType` when loading old data. On write, the first list value is mirrored to the legacy field for old consumers.

Catalog seeds identify the usual physical type of mundane weapons. The campaign catalog editor can assign one or more supported types to a campaign-owned weapon copy.

## Saved roll payload extension

`SavedRollData.damageComponents` is optional and contains up to 8 `{ label?, dice, damageType }` components. Existing `dice` is the base component. The selected base type must belong to the referenced weapon's `damageTypes`; for a weapon with one type it is preselected. Each additional component has an independent subtotal. Damage application continues receiving the existing aggregate total and is not modified by resistance rules.

Invalid dice counts, custom type length, component count, or mismatch between weapon and base type returns the normal validation error envelope. Persisted legacy JSON deserializes with an empty component list.

## Roll result extension

The result contains the existing final total and adds `damageBreakdown: [{ label, damageType, subtotal, dice }]`. The field is omitted or empty on non-damage/check rolls. The server generates all die values; the client cannot submit results.
