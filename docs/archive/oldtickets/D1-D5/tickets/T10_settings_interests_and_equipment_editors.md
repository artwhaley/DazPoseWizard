# T10 — Settings Interests and Equipment Editors

**Roadmap mapping:** D5  
**Depends on:** T07–T09

## Goal

Extend the existing production Settings flow with profile editing for kinks/interests and equipment.

## First inspect

Locate the current main-menu/settings presentation and follow its ownership conventions.
Do not replace the existing Settings screen.
Do not move Intiface/audio/tuning settings.

## Kink catalog

Create a representative v1 kink catalog with stable IDs:

- `edging`
- `orgasm-denial`
- `restraint`
- `anal-play`
- `oral-use`
- `instruction-following`
- `humiliation`
- `sensory-play`

Each definition:
- id
- displayName
- optional themeTags for future filtering

This is sample content, not a permanent taxonomy.

## Interests editor UI

For every catalog entry display:
- name
- frequency choice: `NO`, `1x`, `2x`, `4x`
- sentiment choice rendered as:
  - party face = Loved
  - smile = Neutral
  - scared face = Punishment

If emoji font support is unreliable, use the emoji plus a text label. The semantic enum is authoritative.

Closing/applying the editor commits through `PlayerProfileService` and recalculates submission score.

## Equipment editor UI

For every equipment definition display:
- name
- `Owned / willing to use` checkbox
- role choice allowing:
  - Reward
  - Punishment
  - Both

Implement role with two booleans, not a mutually exclusive enum.

If not owned/willing, role controls may remain visible but the item is ineligible for prepared selection.

Commit through profile service.

## Reuse

The same editor panel classes must be reusable by the scene negotiation prompt in T11. Settings mode and scene mode may differ in title/confirm button but edit the same profile model.

Do not duplicate separate "settings kink UI" and "scene kink UI" implementations.

## Acceptance

The application menu can edit and persist both sets of preferences.
Opening Settings never starts/stops a scene solely because these editors exist.
