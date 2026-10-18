# T02 — Story Scene Bindings and Program Validation

**Roadmap mapping:** D1  
**Depends on:** T01

## Goal

Resolve symbolic story IDs to existing scene objects/assets without putting Unity object references in JSON.

## Add

`Assets/DazPose/Runtime/Story/Runtime/StorySceneBindings.cs`

Use serializable lists:

```csharp
[Serializable] public sealed class StoryTransformBinding {
    public string id;
    public Transform value;
}

[Serializable] public sealed class StoryObjectBinding {
    public string id;
    public UnityEngine.Object value;
}
```

`StorySceneBindings : MonoBehaviour` owns:
- `StoryTransformBinding[] targets`
- `StoryObjectBinding[] assets`
- reference to `StoryPromptService`
- reference to `PlayerBodyPresence` (added in T05; until then field may compile after T05 in same batch)
- optional debug presenter reference

Expose strict methods:
- `Transform RequireTarget(string id)`
- `T RequireAsset<T>(string id) where T : UnityEngine.Object`
- `bool TryGet...` only where callers genuinely need optional behavior

Duplicate IDs or null assigned values are validation errors.

Do not auto-search the hierarchy.

## Add opcode contract validation

Add `StoryProgramValidator.cs`.

The validator has an explicit table/switch of known v1 opcodes and validates required parameters/bindings before a run begins.

D1–D5 v1 opcodes:

### Story/control
- `story.checkpoint`
- `wait.seconds`
- `flow.if`
- `flow.repeat`
- `event.wait`
- `event.emit`

### Lara/performance
- `lara.say`
- `lara.pose`
- `lara.gesture`
- `lara.lookAt`
- `lara.clearGaze`
- `lara.walkTo`
- `lara.sitAt`
- `lara.stand`
- `lara.dissolveInAt`
- `lara.dissolveOut`
- `lara.dissolveTo`
- `lara.magicShader` only if an existing typed vocabulary/effect command already exists by the time this ticket executes; otherwise reject this opcode and omit it from the D1–D5 program rather than bypassing the vocabulary.

### Player
- `player.moveTo`
- `player.lookAt`
- `player.track`
- `player.stopTracking`
- `player.body.dissolveIn`
- `player.body.dissolveOut`

### State/memory
- `state.set`
- `state.add`
- `memory.set`
- `memory.add`
- `local.set`
- `local.add`

### UI/session setup
- `ui.showKinks`
- `ui.showEquipment`
- `ui.showClothing`
- `ui.message`
- `session.calculateSubmissionScore`
- `session.selectPreparedEquipment`

Do **not** add `director.*` or `card.*` opcodes in this packet.

## Validation behavior

Validate:
- opcode known;
- required target/asset IDs present;
- referenced target/asset resolves to correct type;
- nested blocks recursively valid;
- `flow.repeat` count is nonnegative and capped at 1000;
- `wait.seconds` nonnegative;
- state scopes/keys are recognized by the state accessor;
- event/panel IDs are nonblank where required.

## Leave alone

Do not turn `PerformanceSceneBindings` into a generic registry.
Do not move all existing authoring assets into Story bindings.

## Acceptance

A malformed authored program fails before executing step 1, with story ID and the exact invalid step.
