# T07 — Persistent Player Profile and Atomic Storage

**Roadmap mapping:** D4  
**Depends on:** T01

## Goal

Add application-owned long-term player memory separate from existing app settings/tuning.

## Add

Under `Assets/DazPose/Runtime/Application/`:
- `PlayerProfile.cs`
- `PlayerProfileService.cs`

Under `Story/State/`:
- enums/DTOs used by profile.

### File

Persist to:

`Application.persistentDataPath/DazPose/player-profile.json`

Use the same atomic temp/write/replace pattern already used by `GameSettingsService`.

Schema version = 1.

### `PlayerProfile`

Required fields:

```text
version
kinks[]
equipment[]
submissionScore
favor
facts[]
assignments[]
sessionHistory[]   // bounded summary records only; may be empty in D1-D5
```

Defaults:
- submissionScore = 0
- favor = 50

### Kink preference

`KinkPreference`:
- `id`
- `frequency`: integer enum values 0,1,2,4
- `sentiment`: `Loved`, `Neutral`, `Punishment`

Frequency 0 means hard-disabled for future selection.

### Equipment preference

`EquipmentPreference`:
- `id`
- `ownedAndWilling` bool
- `useAsReward` bool
- `useAsPunishment` bool

Both role booleans may be true.
If `ownedAndWilling` is false the item cannot be prepared for a session.

### Generic persistent facts

`PersistentStoryFact`:
- key
- valueType (`string`, `number`, `bool`)
- value fields

This is for sparse long-lived facts. Do not store physical/session state here.

### Assignment placeholder

`AssignmentInstance`:
- unique `instanceId`
- `definitionId`
- ISO-8601 `issuedAtUtc`
- `status`: `Pending`, `Completed`, `Failed`, `Expired`, `Cancelled`
- `StoryArgument[] parameters`

D1–D5 only persist/read it. Assignment gameplay comes later.

### Service ownership

`GameApplication` creates/owns exactly one `PlayerProfileService`, similar to `Settings`.

Expose:
`GameApplication.Current.Profile`

Do not merge it into `GameSettingsService`.

### Save policy

Save immediately when profile editor commits a user change.
Do not write every frame.
On corrupt/malformed file:
- retain a recoverable diagnostic;
- fall back to defaults in memory;
- do not delete the bad file automatically.

## Acceptance

Profile and tuning/settings remain separate files and owners.
No scene owns persistent profile lifetime.
