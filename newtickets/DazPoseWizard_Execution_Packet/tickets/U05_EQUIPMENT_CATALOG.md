# U05 — Dense scalable Equipment editor

**Depends:** U03. **Goal:** Real 100+ catalog equipment management without a 220px-per-item vertical questionnaire.

Reusable compact row with ID/display name, optional details/tags, direct independent toggles: `Owned / willing`, `Use as reward`, `Use as punishment`. Search name/ID/identity/theme tags. Filter All/Owned/Unowned/Reward/Punishment/category. Rows group by optional display category or tags without changing `EquipmentDefinition.id`, identity/theme/exclusion rules. Preserve reward/punishment when owned toggled off unless existing domain imposes explicit constraint; do not auto-activate/prep toy, do not run compatibility checks as user selects preference. Draft Apply/Cancel via U03. Show labels without assuming 50 entries fit simultaneously; use proper viewport and footer.

Tests generated 100/300, filtered edits retain state, independent reward+punishment, owner disable preserve, string search, actual 5 old equipment definitions, long names, resolution bounds, keyboard and profile reload.

**Gate:** One clear UI for 100 items, both caller contexts, no accidental mutations or layout overflow.
