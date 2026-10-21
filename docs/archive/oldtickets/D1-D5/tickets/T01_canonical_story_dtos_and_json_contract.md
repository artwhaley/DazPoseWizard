# T01 — Canonical Story DTOs and JSON Contract

**Roadmap mapping:** D1  
**Depends on:** none

## Goal

Create the canonical `dazpose.story` v1 JSON data model. This ticket does not execute anything.

## Add

Under `Assets/DazPose/Runtime/Story/Serialization/`:

- `StoryProgramDocument.cs`
- `StoryStep.cs`
- `StoryCondition.cs`
- `StoryArgument.cs`
- `StoryProgramSerializer.cs`
- `StoryProgramException.cs`

Namespace: `DazPose.Story`

All DTOs used by `JsonUtility` must be `[Serializable]` classes with fields, not dictionaries or polymorphic inheritance.

### `StoryProgramDocument`

Fields:

```csharp
public string schema = "dazpose.story";
public int version = 1;
public string id;
public string title;
public StoryStep[] steps = Array.Empty<StoryStep>();
```

### `StoryStep`

Use one explicit DTO shape so JsonUtility does not need polymorphism.

Required fields:

```csharp
public string id;           // unique within document; mandatory
public string op;           // string opcode; mandatory
public string note;         // author-readable annotation/dialogue transcript; runtime ignores unless UI opcode uses it

public string target;       // scene target/binding ID
public string asset;        // asset binding ID
public string key;          // state/memory/local key
public string panel;        // UI panel ID
public string eventId;      // event ID
public string value;        // string value
public float number;        // numeric value
public float seconds;       // duration
public bool flag;           // boolean value

public StoryArgument[] args;
public StoryCondition condition;
public StoryStep[] steps;       // nested body
public StoryStep[] elseSteps;   // if/else
```

Do not add 30 specialized fields. `args` handles extra named scalar arguments.

### `StoryArgument`

Fields:
- `name`
- `stringValue`
- `numberValue`
- `boolValue`
- `valueType` with allowed values `string`, `number`, `bool`

### `StoryCondition`

Fields:
- `scope`: `profile`, `contract`, `session`, `world`, `local`
- `key`
- `comparison`: `eq`, `neq`, `gt`, `gte`, `lt`, `lte`, `contains`, `notContains`
- `stringValue`
- `numberValue`
- `boolValue`
- `valueType`

One condition per `flow.if` in v1. Compound AND/OR is intentionally deferred until a real authored need appears; nested `flow.if` supplies the behavior for now.

### Serializer

`StoryProgramSerializer.Parse(string json)`:
- rejects null/blank;
- `JsonUtility.FromJson<StoryProgramDocument>`;
- checks schema/version;
- checks nonblank document ID;
- recursively checks every step has unique nonblank ID and nonblank opcode;
- returns the document.

`ToJson(document, pretty = true)` exists for future editor/compiler use.

No file IO belongs in serializer.

## Leave alone

- `PerformanceSequence`
- `PerformanceRun`
- all existing C# authored sequences
- package manifest

## Acceptance

Source inspection can prove:
- schema is versioned;
- DTO round-trips through JsonUtility;
- nested steps are represented without reflection/polymorphic serializers;
- malformed schema/version/duplicate step IDs produce actionable `StoryProgramException`.

Do not run Play Mode.
