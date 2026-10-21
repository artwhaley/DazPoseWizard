# `dazpose.story` Schema v1 — Author Reference

This is the human-facing shape. Runtime DTO names are authoritative.

## Minimal document

```json
{
  "schema": "dazpose.story",
  "version": 1,
  "id": "example",
  "title": "Example",
  "steps": [
    {
      "id": "line-001",
      "op": "lara.say",
      "asset": "voice:example-001",
      "note": "Human readable transcript."
    },
    {
      "id": "wait-001",
      "op": "wait.seconds",
      "seconds": 0.5
    },
    {
      "id": "done",
      "op": "story.checkpoint",
      "note": "Example complete."
    }
  ]
}
```

## Binding rules

`target` resolves through `StorySceneBindings.targets`.

`asset` resolves through `StorySceneBindings.assets`.

IDs are stable authoring IDs, not hierarchy paths or Unity GUIDs.

## Conditional example

```json
{
  "id": "clothing-branch",
  "op": "flow.if",
  "condition": {
    "scope": "world",
    "key": "clothing.count",
    "comparison": "eq",
    "numberValue": 0,
    "valueType": "number"
  },
  "steps": [
    {
      "id": "naked-mood",
      "op": "state.add",
      "key": "session.mood",
      "number": 5
    }
  ],
  "elseSteps": []
}
```

The implementation may normalize `state.add` scope/key representation, but the canonical source form must remain stable once T01/T09 settle it. Do not add arbitrary expression syntax in v1.

## Future

D6/D7 may add explicit `card.*` and `director.*` opcodes in schema v1 if backward compatible, or bump the schema version if the DTO semantics must change.

A future block editor emits this format. Runtime does not depend on the editor.
