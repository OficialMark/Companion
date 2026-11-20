# Data Model: spec 012

| Entidade | Campo | Tipo | Regra |
|---|---|---|---|
| CatalogSpell | `NameEn` | `nvarchar(120)` null | Vem de `spells.names-en.json` pelo slug |
| CatalogSpell | `SearchText` | `nvarchar(max)` not null | Normalize(Name), Normalize(NameEn) e Normalize(texto da descrição sem HTML), separados por `\|`. Recalculado em `Apply` |
| CatalogItem | `SearchText` | `nvarchar(max)` not null | Mesma regra, com Name, NameEn e Template.Description |

- **Migration**: `AddCatalogSearchText`, só aditiva. As linhas existentes recebem `''`, e o seeder recalcula na subida porque a versão do seed muda.
- **Sem mudança** em fichas, campanhas ou criaturas.
