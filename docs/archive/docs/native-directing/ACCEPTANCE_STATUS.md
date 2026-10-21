# Current authoring disposition — October 19

Art: “Okay. I think that worked.” Practical visual-authoring check accepted by user report; ready to start FirstContact authoring in the main project. Code is already there on main. Main project directions: `docs/story-runtime/SCENE_DIRECTOR_STUDIO_GUIDE.md`; scope/content differences: `docs/visual-authoring/PRODUCTION_HANDOFF.md`.

No individual extended native cases or diagnostic exports are inferred from that report. The detailed old matrix below is historical and includes superseded rehearsal requirements; it is not a request to repeat scenario/RNG/Director checks in the authoring editor. Extended save/Undo/reload/material observations remain unverified where Art has not specifically reported them. Full technical certification is not claimed.

## Historical delivery matrix

> Current correction: responsive visual authoring A00–A06, `docs/visual-authoring/IMPLEMENTATION_REPORT.md`. G4 remains unaccepted. Prior canonical navigation acceptance requirements below are historical.

# Native directing acceptance status

Overall PARTIAL / WAITING ON ART G4 retry. Art rejected the integrated Studio interface; the cue-first correction is source-checked but has not been accepted. Historical G1–G3 user reports remain accepted. No screenshots required. Every remaining mandatory native case is requested by G4_ART_HANDOFF.md and the G4 panel; none is inferred from managed output.

| ID | Requirement | Source / managed | Native |
|---|---|---|---|
| A01 | Existing saved Unity scene selected, no duplicate stage | Source implemented; native evidence required | PASS (G1 user reported) |
| A02 | Real Game view in Edit Mode | Source implemented; native evidence required | PASS (G1 user reported) |
| A03 | Standing end cue | Source implemented; native evidence required | PASS (G2 user reported) |
| A04 | Persistent standing mark editing | Source implemented; native evidence required | PASS (G2 user reported) |
| A05 | Camera endpoint | Source implemented; native evidence required | PASS (G2 user reported) |
| A06 | Persistent target tracking | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A07 | **Three actual Lounger states** | Source implemented; native evidence required | PASS (G3 user reported; integrated replay waits G4) |
| A08 | Seat mark semantics | Source implemented; native evidence required | PASS (G3 user reported) |
| A09 | Other commands retain composition | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A10 | Global card selection untouched | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A11 | Per-encounter overrides | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A12 | Script-before/after/Back | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A13 | Re-anchor without re-execution | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A14 | Save with only preview staged | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A15 | Save with authored mark change | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A16 | Native Undo/Redo | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A17 | Stop/close/reload restoration | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A18 | AnimationMode coexistence | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A19 | Invalid seat/camera/binding | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A20 | No game side effects | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A21 | Preserve content/GUIDs | PASS — source/managed evidence | Source evidence only (not native rendering) |
| A22 | No custom Studio renderer | PASS — source/managed evidence | Source evidence only (not native rendering) |
| A23 | Compiler/Director regression | PASS — source/managed evidence | Source evidence only (not native rendering) |
| A24 | Native import and Editor safety | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A25 | First-use usability | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A26 | Native lifecycle transitions | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A27 | Existing MainMenu/player UI intact | PASS — source/managed evidence | No new native claim; relevant G4 observation still required |
| A28 | Entire native workflow obeys no-Unity-by-executor rule | PASS — source/managed evidence | Source evidence only (not native rendering) |
| A29 | Protected asset/index isolation | PASS — source/managed evidence | Source evidence only (not native rendering) |
| A30 | Runtime seating solver parity | Source implemented; native evidence required | PASS (G3 user reported) |
| A31 | Command-specific facing/pose reset | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A32 | Gesture overlay lifecycle and non-awaited semantics | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A33 | MagicShader/cloned material and wardrobe writer safety | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A34 | Revision separation / laraFocus no loop | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A35 | Fail-fast visual queue | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |
| A36 | Incremental property-family transaction proof | Source implemented; native evidence required | NOT RUN / WAITING ON ART G4 |

Evidence: N10_MANAGED_RESULTS.json (255 passing), N10_SOURCE_COMPILE.json (seven passing), N10_ASSET_AUDIT.json (zero issues), PROTECTED_CHECK.json (7,211 originals unchanged), N09_RETIREMENT_AUDIT.json (zero old renderer source matches). N11 remains held at G4.
