# Implementation Plan — Desktop Predictive Typing Assistant

> **Platform:** Windows 11, initially  
> **Primary objective:** Build a reliable, low-latency, privacy-preserving desktop typing assistant that provides up to three word-completion or next-word suggestions near the active caret.  
> **Planning principle:** Prove input-integration feasibility before committing to the architecture. Keep prediction independent of Windows integration and UI. Treat compatibility as something to test and document—not assume.

This plan is derived from `context.md`. Technologies and architecture choices described as provisional must be validated through experiments before they become commitments.

---

## 1. Delivery Strategy

Build the product in this order:

1. Prove that a Windows integration approach can obtain enough text/caret context and safely apply an explicit user-accepted edit.
2. Build and benchmark a standalone dictionary-completion engine.
3. Implement context tracking and connect it to the selected Windows input adapter.
4. Add the non-activating suggestion popup and safe acceptance/dismissal behavior.
5. Add contextual next-word prediction only after the baseline is stable.
6. Expand compatibility based on test evidence.
7. Add optional personalization, complete privacy/settings controls, and harden packaging and release behavior.

Do **not** start by building a polished UI around an unproven keyboard hook. A low-level keyboard hook can observe events, but it does not by itself guarantee access to the text around the caret or reliable caret coordinates in every application.

### Definition of a stage

A stage is complete only when its exit criteria are demonstrated by tests, a reproducible experiment, or a documented decision. Compilation alone does not establish correct system-wide typing behavior.

---

## 2. Proposed Repository Structure

Create directories when they are needed by a concrete stage; do not create empty folders or abstractions in advance. This is a target structure, not a requirement to implement every component immediately.

```text
desktop-typing-assistant/
├── README.md
├── context.md
├── plan.md
├── LICENSE
├── .gitignore
├── docs/
│   ├── architecture-decisions.md
│   ├── compatibility-matrix.md
│   ├── privacy-model.md
│   ├── benchmarks.md
│   ├── testing-guide.md
│   └── release-checklist.md
├── app/
│   ├── shell/                 # Application lifecycle, tray/status, startup
│   ├── settings/              # User settings and privacy controls
│   └── popup/                 # Suggestion presentation and interaction
├── input/
│   ├── interfaces/            # Contracts shared by input adapters
│   ├── windows-tsf/           # TSF implementation, if validated
│   ├── keyboard-prototype/    # Isolated feasibility prototype; not assumed final
│   └── adapters/              # Specific accessibility/app adapters if justified
├── prediction/
│   ├── dictionary/             # Dictionary loading and prefix lookup
│   ├── ranking/                # Candidate scoring and deduplication
│   ├── context/                # Word boundary and recent-context logic
│   └── inference/              # N-gram/model interfaces and local inference
├── personalization/            # Optional local adaptation, introduced late
├── data/
│   └── README.md               # Data sources, formats, provenance and licenses
├── models/
│   └── README.md               # Model provenance, licensing and loading notes
├── tests/
│   ├── unit/
│   ├── integration/
│   ├── compatibility/
│   └── test-data/
├── tools/
│   ├── feasibility/            # Small Windows API experiments
│   └── evaluation/             # Offline quality and performance evaluation
└── packaging/                  # Installer and uninstall assets, introduced late
```

### Repository conventions

- Keep `prediction/` independent of the Windows input layer and popup technology.
- Put decisions and evidence in `docs/architecture-decisions.md`; do not leave important API assumptions only in code comments.
- Keep `docs/compatibility-matrix.md` updated as applications are tested.
- Do not commit private user text, raw keystroke logs, or unlicensed dictionaries/models.
- Add build files, project files, and test projects appropriate to the technologies actually selected. The exact solution layout depends on the feasibility results.
- A likely starting direction is C#/.NET for the application shell and settings, with native Windows code where an API requires it; Python can be used for offline model experiments. This is provisional, not a pre-decided architecture.

---

## Stage 0 — Requirements Baseline and Windows API Feasibility

### Goal

Establish what can reliably be observed and edited in representative Windows text fields before choosing the production input integration path.

### Tasks

1. Review the product requirements and turn them into a concise test checklist.
2. Investigate:
   - **Text Services Framework (TSF):** text context, caret access, insertion/edit operations, lifecycle, registration and deployment.
   - **Low-level keyboard hook:** event observation, limitations, lifecycle and security implications.
   - **Accessibility APIs:** availability of text/caret information in selected applications.
   - **Overlay options:** non-activating window behavior, positioning and focus handling.
3. Build a small feasibility harness for a controlled editor. It should report whether the chosen API can:
   - detect relevant text changes;
   - obtain the current word and a bounded preceding context;
   - obtain caret location where available;
   - identify focus/context changes;
   - apply an explicitly requested edit without duplicating or dropping characters.
4. Test a small representative set of application categories, such as a basic Win32 text editor, a browser text field, an office editor, VS Code and a terminal.
5. Record each experiment, Windows/API assumptions, limitations, permissions, packaging implications and unresolved questions.
6. Decide whether the first implementation uses TSF, a limited prototype plus another adapter, or a documented hybrid. Do not claim broad compatibility based on one successful editor.

### Likely files to add or modify

- `docs/architecture-decisions.md`
- `docs/compatibility-matrix.md`
- `docs/privacy-model.md`
- `tools/feasibility/` — small, disposable experiments
- `input/keyboard-prototype/` — only if a keyboard-hook prototype is useful
- `README.md` — initial build and experiment instructions

### Expected output

- A written architecture decision with evidence and trade-offs.
- A working proof of concept that reads enough context and performs a verified edit in at least one controlled application.
- An initial compatibility matrix with tested, unsupported and unknown behaviors clearly separated.

### Exit criteria

- The chosen first integration path has demonstrated context acquisition and text modification in a controlled editor.
- The team understands what the path cannot reliably do.
- A safe fallback is defined for unknown, sensitive or unsupported fields.
- No claim of universal application support is made.

### Dependencies and risks

This stage gates the production input architecture. If caret/context access or safe editing cannot be demonstrated, revise the approach before investing in the popup or full application shell.

---

## Stage 1 — Standalone Dictionary Completion Engine

### Goal

Deliver a deterministic prediction library that suggests up to three likely completions for a partial word, without any Windows input integration or UI dependency.

### Tasks

1. Select a dictionary source with word-frequency information; document its provenance and license.
2. Define a dictionary format and robust loading behavior. Reject malformed input safely and avoid unbounded memory/resource consumption.
3. Implement prefix lookup using a suitable structure, such as a trie, sorted dictionary or finite-state structure. Choose based on measurements rather than preference alone.
4. Normalize lookup case while preserving appropriate output casing.
5. Rank candidates using prefix match and frequency; deduplicate candidates and cap results at three.
6. Define behavior for empty input, unknown prefixes, punctuation boundaries, numbers, technical terms and Unicode.
7. Create a small command-line demonstration that accepts a prefix and prints ranked candidates.
8. Benchmark dictionary loading and lookup time on the development machine.
9. Add unit tests before integrating the engine with the Windows layer.

### Likely files to add or modify

- `prediction/dictionary/` — dictionary loader, prefix index and candidate source
- `prediction/ranking/` — initial frequency-based ranking and deduplication
- `tests/unit/` — dictionary and ranking tests
- `tools/evaluation/` — lookup benchmark
- `data/README.md` — dictionary provenance, format and licensing
- `README.md` — instructions for running the CLI demo

### Expected output

A standalone library and CLI demo that produce stable top-three completions from a supplied prefix. It should run independently of the desktop UI and input integration.

### Exit criteria

- Tests cover empty input, unknown prefixes, duplicates, case handling, punctuation boundaries, Unicode and malformed dictionary data.
- The dictionary lookup target is measured; the initial target is **under 30 ms** at the prediction-service boundary on the development machine.
- Memory use and initialization time are recorded.
- Dictionary/model data licensing is documented.

### Dependencies and risks

Can be developed while Stage 0 research proceeds, provided no production input API assumptions leak into the prediction library. Dictionary quality and licensing affect results and distribution.

---

## Stage 2 — Input Adapter Contract and Context Manager

### Goal

Track the minimum context required for prediction and connect it to the chosen input integration without retaining whole documents or stale text.

### Tasks

1. Define a small input-adapter contract for relevant text/context updates, caret/selection changes, focus changes and text-edit requests. Keep API-specific details behind the adapter.
2. Implement context state that distinguishes:
   - a partially typed current word;
   - a boundary where next-word prediction may be useful;
   - invalid or unavailable context.
3. Handle typing, backspace, whitespace, punctuation, selection replacement, caret movement and focus changes.
4. Keep only the minimum recent context needed by prediction. Clear or invalidate it when focus or field identity changes where appropriate.
5. Define how composition/IME input is handled or conservatively excluded until supported.
6. Ensure sensitive fields are excluded when they can be identified. If sensitivity is unknown, follow the conservative policy documented in the privacy model.
7. Add cancellation/versioning so an old context update cannot be treated as current after later input.
8. Test the context manager with simulated input events before relying on live application integration.

### Likely files to add or modify

- `input/interfaces/` — input and text-editing contracts
- `input/windows-tsf/` or the validated adapter location
- `prediction/context/` — context state and word-boundary logic
- `tests/unit/` — context transitions and boundary behavior
- `tests/integration/` — adapter/context scenarios
- `docs/privacy-model.md`
- `docs/architecture-decisions.md`

### Expected output

A context manager that converts supported input events into a small, well-defined prediction request and invalidates stale context safely.

### Exit criteria

- A defined test suite passes for typing, backspace, punctuation, selection, caret movement and focus changes.
- Context is not carried into a different field when it should be cleared.
- No raw keystroke stream or complete typed document is logged or persisted.
- Unsupported context is represented explicitly instead of guessed.

### Dependencies and risks

Depends on Stage 0's selected integration path. Application-specific APIs may expose different amounts of context; the contract must allow unavailable information without pretending it exists.

---

## Stage 3 — Suggestion Popup and Explicit Acceptance

### Goal

Show suggestions near the caret and make accepting or dismissing them predictable, without stealing focus or corrupting text.

### Tasks

1. Build a minimal popup displaying zero to three candidates.
2. Investigate and implement non-activating behavior so the popup does not take keyboard focus from the active editor.
3. Position the popup using verified caret coordinates when available. Define fallback behavior when coordinates cannot be obtained reliably.
4. Define keyboard behavior:
   - configurable acceptance shortcut;
   - Escape to dismiss;
   - predictable behavior when a suggestion is unavailable;
   - no interception of unrelated keystrokes.
5. Add mouse selection only where it can be implemented without activating the popup in a way that disrupts typing.
6. Route accepted suggestions through the text-editing adapter. Verify the edit where possible; do not report success when the operation fails or cannot be verified.
7. Prevent feedback loops from the assistant's own text insertion.
8. Handle stale results, rapid typing, focus loss, popup dismissal and adapter failures.
9. Test insertion at the beginning, middle and end of a word, including punctuation and trailing spaces.

### Likely files to add or modify

- `app/popup/` — popup window, candidate presentation and interaction
- `app/shell/` — lifecycle/status integration as needed
- `input/interfaces/` — explicit accept/edit operation contract
- `input/windows-tsf/` or chosen adapter — edit implementation
- `tests/integration/` — popup, focus and acceptance cases
- `docs/compatibility-matrix.md`

### Expected output

A usable end-to-end prototype: type in a supported editor, see suggestions near the caret, accept one deliberately, or dismiss it without disrupting normal typing.

### Exit criteria

- Acceptance tests show no duplicated or missing characters in the tested scenarios.
- The popup does not steal focus during ordinary typing in supported test cases.
- Escape, punctuation, focus changes and caret/context changes update or dismiss the popup predictably.
- Stale prediction results never replace newer suggestions.
- Failure to apply an edit is handled safely and is not reported as success.

### Dependencies and risks

Depends on Stage 0 input feasibility and Stage 2 context behavior. Popup positioning and non-activation may vary across Windows applications and display configurations; document limitations.

---

## Stage 4 — End-to-End Dictionary Completion and Performance Baseline

### Goal

Make dictionary completion reliable as a complete desktop experience before adding more complex prediction.

### Tasks

1. Connect context updates to the Stage 1 dictionary engine.
2. Add debouncing/cancellation only as needed; avoid delaying the UI perceptibly.
3. Ensure each prediction request has a version or equivalent freshness marker so results from old requests are discarded.
4. Define confidence and empty-result behavior so low-quality suggestions can be suppressed.
5. Add a visible global enabled/paused state and a reliable way to pause or disable the assistant.
6. Measure the full path separately from dictionary lookup: context acquisition, lookup, ranking, result delivery and UI update.
7. Record median and p95 latency, startup time, memory use and CPU use.
8. Test offline operation and verify that no network access is required for ordinary prediction.

### Likely files to add or modify

- `app/shell/` — enabled/paused state and status
- `app/popup/` — loading/empty/stale-result behavior as needed
- `prediction/` — integration interfaces and request freshness
- `tests/integration/`
- `tools/evaluation/`
- `docs/benchmarks.md`
- `docs/privacy-model.md`

### Expected output

A stable, measurable dictionary-completion experience in the first supported editor(s), with predictable pause, dismissal and acceptance behavior.

### Exit criteria

- The Stage 1 lookup benchmark and end-to-end latency are reported separately.
- Median and p95 latency, memory and CPU use are measured.
- Offline completion works.
- Rapid typing and focus changes do not surface stale candidates.
- No raw keystrokes or full typed documents are written to logs.

### Dependencies and risks

This stage is the baseline against which contextual prediction will be evaluated. Do not proceed to a more complex model to hide input integration or UI latency problems.

---

## Stage 5 — Next-Word Prediction

### Goal

Add context-aware next-word suggestions while preserving the responsiveness, privacy and stability of dictionary completion.

### Tasks

1. Define when next-word prediction is eligible—for example, after a word boundary—and how it differs from partial-word completion.
2. Implement a lightweight n-gram baseline.
3. Create or select an appropriate held-out evaluation dataset. Do not use private user documents without explicit permission and privacy review.
4. Measure top-1 and top-3 prediction quality, initialization time, per-prediction latency, memory and CPU use.
5. Evaluate a compact neural language model only if the n-gram baseline leaves a meaningful quality gap.
6. If evaluating a model, compare local inference options such as ONNX Runtime or Windows ML based on measured quality, latency, memory, packaging complexity, hardware support and licensing.
7. Keep inference local and normal prediction independent of network connectivity.
8. Combine dictionary and contextual evidence through a ranking layer; deduplicate and cap output at three.
9. Add cancellation and freshness checks so slower inference cannot display stale suggestions.
10. Choose the simplest model that meets the measured quality and performance targets.

### Likely files to add or modify

- `prediction/context/` — next-word eligibility and context preparation
- `prediction/inference/` — n-gram baseline and optional local-model adapter
- `prediction/ranking/` — combined ranking
- `models/README.md`
- `data/README.md`
- `tests/unit/` — ranking and model-interface tests
- `tests/integration/` — end-to-end contextual prediction
- `tools/evaluation/` — top-1/top-3 and latency evaluation
- `docs/benchmarks.md`

### Expected output

A measured next-word prediction implementation, beginning with an n-gram baseline, plus a report explaining whether a compact neural model provides enough improvement to justify its costs.

### Exit criteria

- Evaluation reports top-1/top-3 quality and performance measurements.
- Contextual prediction improves on the baseline by a documented, meaningful amount without unacceptable latency or resource use.
- Normal prediction remains offline.
- Stale results are cancelled or discarded.
- Model and dataset licenses/provenance are documented.

### Dependencies and risks

Depends on a reliable end-to-end dictionary baseline. Model size, startup cost, inference latency, licensing and dataset representativeness may outweigh a small accuracy improvement.

---

## Stage 6 — Compatibility Expansion

### Goal

Expand the set of supported applications through reproducible testing, not assumptions.

### Tasks

1. Establish a repeatable compatibility test procedure.
2. Test representative versions of browsers, Word or similar office software, VS Code, messaging applications and terminals.
3. For each application and field type, record whether the assistant can obtain context, locate the caret, insert text reliably, avoid stealing focus and dismiss correctly.
4. Test normal and rapid typing, backspace, selection, punctuation, focus changes and acceptance.
5. Add a dedicated compatibility adapter only when testing demonstrates a specific need and the maintenance cost is justified.
6. Define conservative exclusions for password/sensitive fields and fields whose sensitivity or editability cannot be determined reliably.
7. Document policies and limitations for remote desktop, elevated applications, games and custom-rendered editors.
8. Run regression tests after changes to adapters or popup behavior.

### Likely files to add or modify

- `docs/compatibility-matrix.md`
- `input/adapters/` — only justified application-specific adapters
- `tests/compatibility/`
- `tests/integration/`
- `docs/testing-guide.md`

### Expected output

A published compatibility matrix that distinguishes supported, partially supported, unsupported and untested application scenarios.

### Exit criteria

- Every compatibility claim is backed by a recorded test for the stated application/version and field category.
- Unsupported contexts fail safely rather than causing unexpected edits.
- Adapter-specific regressions are covered by repeatable tests.
- Known limitations are visible to users.

### Dependencies and risks

Compatibility work may reveal architectural limitations that require revisiting Stage 0 decisions. Do not label an entire application as supported if only one field type has been tested.

---

## Stage 7 — Settings, Privacy Controls and Optional Personalization

### Goal

Give users clear control over activation, shortcuts, exclusions, privacy and any retained personalization data.

### Tasks

1. Add settings for:
   - global enable/disable;
   - pause shortcut and suggestion-acceptance shortcut;
   - supported-app exclusions where available;
   - model choice if more than one model is shipped;
   - personalization enable/disable.
2. Detect and document shortcut conflicts; provide safe behavior if a shortcut cannot be registered or conflicts with another application.
3. Add an optional local personalization mechanism based on explicit acceptance/rejection signals and/or local word frequency.
4. Do not store complete typed documents. Define exactly what personalization data is stored and its retention limits.
5. Make personalization optional, visible, resettable and disabled when the user opts out.
6. Add a control to clear all local personalization data.
7. Use SQLite or another lightweight local store only if persistence is required; keep the schema and data scope minimal.
8. Confirm password/sensitive-field exclusions and the conservative policy for uncertain fields.
9. Ensure all ordinary prediction continues to work offline and no typed content is transmitted to a cloud service.

### Likely files to add or modify

- `app/settings/`
- `app/shell/` — global status and pause behavior
- `personalization/`
- local settings/store implementation, if needed
- `tests/unit/` — opt-out, reset and retention behavior
- `tests/integration/` — shortcut and settings behavior
- `docs/privacy-model.md`
- `docs/architecture-decisions.md`

### Expected output

A usable settings surface with reliable pause/disable behavior, transparent privacy controls and optional, locally stored personalization.

### Exit criteria

- Users can disable the assistant globally and resume it predictably.
- Users can disable personalization and clear its stored data.
- The application documents what it stores and why.
- No raw keystroke logs or complete typed documents are retained.
- Sensitive-field handling is tested and limitations are documented.

### Dependencies and risks

Introduce personalization only after core completion and next-word prediction are dependable. Poorly bounded personalization can expose sensitive information or overwhelm contextual relevance.

---

## Stage 8 — Hardening, Packaging and Release Candidate

### Goal

Prepare a reproducible Windows 11 release candidate with documented behavior, performance, privacy and installation lifecycle.

### Tasks

1. Test clean installation, first launch, startup behavior, upgrade, crash recovery and uninstall.
2. Verify that uninstall and data-reset behavior match the documented retention policy.
3. Review dependency, dictionary and model licenses; document all external dependencies and network behavior.
4. Test malformed or unexpectedly large dictionary/model files and ensure failures are controlled.
5. Run long-duration typing tests, performance tests, focus-change tests and regression tests.
6. Measure startup time, working-set memory, CPU use, prediction latency and model initialization time.
7. Verify pause/disable behavior after errors and application restarts.
8. Check that the app does not require elevated privileges unless a documented, demonstrated requirement exists.
9. Add reproducible build and test instructions.
10. Complete the release checklist and publish known limitations and the compatibility matrix.

### Likely files to add or modify

- `packaging/`
- `README.md`
- `docs/release-checklist.md`
- `docs/testing-guide.md`
- `docs/compatibility-matrix.md`
- `docs/privacy-model.md`
- `docs/benchmarks.md`
- build/CI configuration and regression tests

### Expected output

A release candidate with installation/uninstallation instructions, reproducible build steps, automated tests, performance results, privacy documentation and a compatibility matrix.

### Exit criteria

The release candidate meets the product definition of done:

- Windows integration strategy is documented and tested.
- Supported application behavior is backed by compatibility tests.
- Suggestions are useful and measured against a baseline.
- UI behavior and resource use are measured.
- Text insertion and dismissal are predictable in tested scenarios.
- Ordinary prediction works offline.
- Privacy controls are present and raw-keystroke logging is absent.
- Installer and clean uninstall are tested.
- Known limitations, automated tests and reproducible build instructions are available.

### Dependencies and risks

Packaging can expose registration, permissions, runtime and model-distribution problems. Test installation and removal on a clean environment rather than only on the development machine.

---

## 3. Cross-Stage Testing Plan

### Unit tests

Maintain focused tests for:

- dictionary parsing, malformed input and prefix lookup;
- ranking, deduplication and candidate limits;
- case normalization and output casing;
- Unicode and punctuation handling;
- context extraction and word-boundary transitions;
- confidence thresholds and empty-result behavior;
- stale-result cancellation;
- personalization opt-out and reset;
- bounded resource use when loading dictionaries/models.

### Integration tests

Test:

- typing, backspace, selection replacement and caret movement;
- focus and application changes;
- accepting suggestions at the beginning, middle and end of a word;
- punctuation and trailing spaces;
- fast typing and rapid focus changes;
- popup focus behavior;
- edit failures and verification behavior;
- prevention of feedback loops caused by assistant-inserted text;
- pause/disable and sensitive-field behavior.

### Compatibility matrix fields

For every tested application scenario, record:

| Field | What to record |
|---|---|
| Application/version | Exact application and tested version |
| Field type | The text field/editor category |
| Context access | Available, partial, unavailable |
| Caret location | Available, partial, unavailable |
| Text insertion | Reliable, limited, unreliable |
| Popup behavior | Correct positioning and focus behavior |
| Sensitive-field behavior | Tested result and limitations |
| Regression status | Test date/result and known issues |

### Performance and quality measurements

Measure separately:

- dictionary lookup latency;
- end-to-end latency, including context acquisition, inference, ranking and UI update;
- median and p95 latency;
- model initialization time;
- working-set memory and CPU use;
- top-1 and top-3 quality on an appropriate held-out dataset;
- controlled user acceptance rate when a suitable evaluation is possible.

Do not treat a single average latency or successful demo as sufficient evidence of reliability.

---

## 4. Engineering Workflow for Each Stage

For each stage, use a small, verifiable implementation cycle:

1. Read `context.md`, this plan and the current codebase.
2. State the stage, scope and acceptance criteria before editing.
3. Inspect relevant official documentation for platform-specific assumptions.
4. Propose the smallest implementation plan that satisfies the stage.
5. Make focused changes; avoid rewriting unrelated components.
6. Build and run the relevant tests.
7. Report commands run, actual outcomes, failures and unverified assumptions.
8. Update the compatibility matrix, architecture decisions and benchmarks where applicable.
9. Stop for human review if an API limitation or architectural decision changes the product's safety, privacy or compatibility assumptions.

### Suggested commit boundaries

Prefer commits that correspond to independently verifiable changes, such as:

- `docs: record Windows input feasibility findings`
- `feat(prediction): add dictionary prefix lookup`
- `test(context): cover caret and punctuation transitions`
- `feat(popup): add non-activating suggestion window`
- `feat(prediction): add n-gram next-word baseline`
- `docs(compatibility): record tested editor support`
- `feat(settings): add global pause and privacy controls`

These are examples, not mandatory commit names.

---

## 5. Explicitly Deferred Features

Do not make these prerequisites for the first dependable release:

- autocorrect;
- multilingual prediction;
- grammar assistance;
- sophisticated personalization;
- broad application-specific customization;
- import/export of personal dictionaries;
- automatic update infrastructure.

Consider them only after the core experience is reliable. In particular, autocorrect must be conservative, must not silently replace text by default, and must preserve technical vocabulary, identifiers, names, URLs and code.

---

## 6. Key Decisions to Resolve With Evidence

Keep these open until experiments or benchmarks support a decision:

1. Is TSF sufficient as the primary integration path, or are specific adapters required?
2. Which popup technology best supports non-activation, positioning and accessibility?
3. Which local prediction model, if any, provides meaningful improvement over an n-gram baseline?
4. Which language should be supported first?
5. Which exact applications and field types define the first release?
6. Which acceptance/pause shortcuts are appropriate, and how should conflicts be handled?
7. What is the policy for remote desktop, elevated applications, games, terminals and custom editors?
8. What memory, CPU, startup and latency budgets are acceptable on the target PC?

Record decisions, evidence and consequences in `docs/architecture-decisions.md`.

---

## 7. Recommended First Work Session

Start with **Stage 0**, not with the full app shell.

1. Create the repository and `README.md`.
2. Add `docs/architecture-decisions.md` and `docs/compatibility-matrix.md`.
3. Select a controlled editor for initial experiments.
4. Compare TSF and a keyboard-hook prototype against the same context-acquisition and edit-verification tests.
5. Record what is proven, what failed and what remains unknown.
6. In parallel, begin Stage 1's standalone dictionary-engine spike only if it does not depend on unverified Windows integration assumptions.

The immediate objective is not to make the assistant look finished. It is to prove that the core interaction can be implemented safely, then build the prediction and UI layers around that evidence.
