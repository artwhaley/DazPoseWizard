# Locked Architecture Decisions

Do not revisit these decisions during D1–D5.

## 1. Canonical authored representation

Use **versioned JSON TextAssets** as the canonical story/card program representation for this sprint.

- schema identifier: `dazpose.story`
- schema version: `1`
- direct human authoring is supported;
- future block editors compile to this same representation;
- do not add YAML, XML, SQLite, Newtonsoft JSON, a parser generator, Roslyn scripting, reflection-based invocation, or a second execution engine.

Use Unity `JsonUtility`-compatible DTOs. The repository already uses `JsonUtility` for application persistence.

## 2. Existing execution path remains authoritative

Serialized stories run through a new `StoryProgramSequence : PerformanceSequence`.

`PerformanceHost` remains the saved-scene lifecycle owner.

The Story layer must call the existing `PerformanceRun` scoped APIs. It must not directly reach into Lara skeletons, audio queues, locomotion internals, toy backends, or camera internals.

## 3. No reflection command dispatcher

Every story opcode is explicitly registered/handled in source.

Unknown opcode = clear `StoryProgramException` including:
- story ID;
- step ID;
- opcode;
- short reason.

This is intentionally boring and debuggable.

## 4. Stable symbolic IDs

Story JSON refers to:
- scene targets by stable string ID;
- Unity assets by stable string ID;
- profile/catalog entries by stable string ID;
- state/memory keys by explicit string ID.

The JSON never contains Unity GUIDs, scene object instance IDs, or hierarchy paths.

A saved `StorySceneBindings` component maps symbolic IDs to scene objects/assets.

## 5. State scopes

Keep these scopes distinct:

### Persistent profile
Survives application restarts:
- kink preferences;
- equipment ownership/willingness/use role;
- submission score;
- long-term generic story facts;
- active assignments/history placeholders.

### Session contract
Snapshot/choices for tonight:
- enabled/current kink preferences;
- prepared equipment;
- pacing modifier;
- negotiated options.

### Session/world state
Reset for a new session:
- phase;
- phase completion;
- mood;
- intensity modifier;
- current clothing;
- posture/occupancy/restriction facts;
- active equipment;
- recent transient story values.

### Scene locals
Scratch variables owned by the current story run.

Do not flatten these into one global dictionary.

## 6. Equipment extensibility

Use a hybrid model:

### Identity tags
What the item is / what cards may explicitly support:
- `equipment:rope`
- `equipment:butt-plug`
- `equipment:dildo-anal`

### Theme tags
What flavor/categories it participates in:
- `theme:buttstuff`
- `theme:restraint`
- `theme:punishment`
- `theme:sensory`

### Excludes tags
Tags that cannot coexist with this equipment.

Compatibility is **symmetric at evaluation time**:
A conflicts with B when A excludes any tag exposed by B OR B excludes any tag exposed by A.

This lets a future item reject old items without editing old definitions.

### Universal physical state
Only durable cross-content truths become state:
- posture;
- hands restriction/occupation;
- ankles restriction;
- oral/anal/genital occupancy;
- clothing slots.

Do not add `SpreaderEquipped`, `RopeEquipped`, etc. as universal booleans. Equipment identity remains data/tags.

## 7. Owned / prepared / active equipment are separate

- **Owned/willing:** persistent profile.
- **Prepared:** selected for the current session.
- **Active:** physically in use right now.

Do not conflate them.

## 8. Player profile vs current Lara mood

Submission score is a persistent/derived player-profile value.
Lara's current mood is session state, default **50**.

They are not the same number.

## 9. D1–D5 is not the Director sprint

No card selector.
No weighted content picker.
No utility AI.
No mood-to-intent policy.
No Director implementation.
No assignment follow-up gameplay.

Only create the seams/data required for D6/D7.

## 10. Block editor is later

Do not integrate Blocks Engine or build an editor graph now.

The future editor will emit the canonical JSON program defined here.
