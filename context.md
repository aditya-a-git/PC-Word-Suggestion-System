# Project Context: Desktop Predictive Typing Assistant

## 1. Project Summary

Build a reliable desktop typing assistant that brings the useful parts
of mobile-keyboard word suggestions to a PC. While a user types, the app
should show up to three relevant suggestions near the active text caret.
The user can accept a suggestion with a configurable keyboard shortcut
or by selecting it.

The goal is not merely to display autocomplete candidates. The product
should feel fast, unobtrusive, context-aware, predictable,
privacy-preserving, and dependable in the applications it explicitly
supports.

**Initial target:** Windows 11 desktop.

**Core product principle:** reliability and user trust take priority
over adding features quickly.

## 2. Product Vision and User Experience

### Core experience

-   Show up to three useful suggestions while the user types.
-   Support both:
    -   **Word completion:** complete the partially typed word,
        e.g. `develop` → `developer`, `development`, `developing`.
    -   **Next-word prediction:** use preceding text to suggest the next
        word, e.g. `I'll see you` → `tomorrow`, `soon`, `there`.
-   Let the user accept a suggestion through a configurable keyboard
    shortcut and, where practical, mouse selection.
-   Position the popup near the caret without stealing focus or
    obscuring the text being edited.
-   Update or dismiss suggestions when the user types, moves the caret,
    changes focus, presses Escape, or enters punctuation.
-   Avoid unexpected edits. Suggestions should normally be inserted only
    after explicit acceptance.
-   Provide settings to enable/disable the assistant, configure
    shortcuts, control personalization, and manage privacy.
-   Operate offline for ordinary prediction; network access must not be
    required for normal typing.

### Future capabilities

Consider these only after the core typing experience is dependable:

-   Conservative autocorrect.
-   Personal vocabulary and phrase adaptation.
-   Multilingual prediction.
-   Per-application behavior and exclusions.
-   Optional grammar assistance.
-   Import/export of user dictionaries.
-   Installer, update mechanism, accessibility improvements, and
    polished settings UI.

## 3. Target Platform and Compatibility

### Initial platform

Target Windows 11 first. Do not promise universal compatibility.
Text-input behavior differs across Win32 applications, browsers, Office
applications, terminals, games, elevated applications, remote desktops,
and custom-rendered editors.

Build a documented compatibility matrix and test each application
category directly.

### Windows text integration

Evaluate the following approaches before committing to one:

1.  **Text Services Framework (TSF) / text-service integration**
    -   Investigate as the primary long-term path for reliable access to
        text context, caret information, and text insertion in supported
        applications.
    -   Requires careful study of Windows text-service APIs, lifecycle,
        registration, deployment, and compatibility.
    -   Do not assume every application exposes the same capabilities
        through TSF.
2.  **Low-level keyboard hook plus overlay**
    -   Useful for an early prototype and for observing keyboard events.
    -   A hook does not, by itself, provide the complete text around the
        caret or reliable caret coordinates in every application.
    -   It must not be treated as a complete or universally reliable
        text integration layer.
    -   Avoid storing raw keystrokes or collecting text unrelated to
        active prediction.
3.  **Accessibility APIs / application-specific integrations**
    -   Investigate where needed for particular application categories.
    -   Treat these as compatibility adapters, not evidence that all
        applications are supported.

Choose the integration approach based on tested reliability, not
implementation convenience alone. Document known limitations and provide
a safe disable/fallback behavior when text context cannot be obtained
reliably.

## 4. Proposed Technology Direction

These are starting recommendations, not irreversible decisions.

-   **Application shell and settings:** C# / .NET. Consider WinUI 3 for
    conventional application windows and settings.
-   **Windows integration:** Windows APIs, with TSF investigated for the
    robust text-input path. Native C++ components may be appropriate
    where required by the API or performance.
-   **Prediction research and evaluation:** Python for experiments,
    dataset preparation, model comparison, and offline evaluation.
-   **Local inference:** Evaluate ONNX Runtime and/or Windows ML for
    compatible models. Select based on measured latency, memory use,
    packaging complexity, supported hardware, and licensing.
-   **Dictionary completion:** Trie, sorted dictionary, finite-state
    structure, or another measured approach; use frequency and
    contextual ranking.
-   **Local settings and personalization:** SQLite or a similarly
    lightweight local store.
-   **Build and packaging:** Use the native build and packaging tools
    appropriate to the chosen components.
-   **Testing:** Unit tests for core logic plus Windows integration and
    end-to-end tests.

Avoid choosing a technology solely because it is familiar. Keep the
prediction engine independent of the Windows input layer and UI so these
can evolve separately.

## 5. Proposed Architecture

Keep components modular:

1.  **Input integration adapter**
    -   Tracks the active input context and receives relevant text
        changes or keyboard events.
    -   Determines whether the current field is eligible for
        suggestions.
    -   Handles focus changes, composition, selection, and caret changes
        to the extent supported by the integration method.
2.  **Context manager**
    -   Maintains only the minimum recent text required for prediction.
    -   Distinguishes a partial current word from a next-word prediction
        context.
    -   Handles whitespace, punctuation, backspace, selection changes,
        and invalidated context.
    -   Clears context when the active application or field changes,
        where appropriate.
3.  **Prediction service**
    -   Produces candidates from a dictionary and a contextual model.
    -   Ranks and deduplicates candidates.
    -   Returns confidence or ranking information and a bounded number
        of suggestions.
    -   Supports cancellation or dropping stale requests so old
        predictions do not appear after the user has continued typing.
4.  **Ranking and personalization**
    -   Combines word frequency, prefix match, context likelihood, and
        optional local user preferences.
    -   Prevents personal frequency from overwhelming context or
        producing repetitive suggestions.
    -   Can be disabled or reset by the user.
5.  **Suggestion UI**
    -   Displays up to three candidates near the caret.
    -   Does not steal keyboard focus.
    -   Repositions or dismisses when the caret/context changes.
    -   Has explicit accept, dismiss, and navigation behavior.
    -   Remains responsive when the model is slow or unavailable.
6.  **Text-editing adapter**
    -   Applies a candidate as a deliberate text operation where
        supported.
    -   Preserves punctuation, selection, and caret behavior.
    -   Prevents the app's own inserted text from triggering a feedback
        loop.
    -   Must not claim success if the insertion operation fails or
        cannot be verified.
7.  **Settings and privacy**
    -   Provides a global enable/disable control, shortcut
        configuration, supported-app exclusions, model selection, and
        personalization controls.
    -   Makes data retention clear.
    -   Keeps prediction local by default.

## 6. Prediction Strategy

Use a hybrid approach instead of requiring a large language model for
every feature.

### Stage A: Dictionary completion

-   Load a general-language dictionary with word-frequency information.
-   Normalize case for lookup while preserving appropriate output
    casing.
-   Return candidates matching the typed prefix.
-   Rank candidates by frequency and later by contextual evidence.
-   Support Unicode deliberately; do not assume every language can be
    represented by simple byte-wise character handling.

### Stage B: Next-word prediction

-   Begin with an n-gram or other lightweight baseline.
-   Evaluate a compact neural language model if it provides a meaningful
    quality improvement.
-   Keep inference local.
-   Measure model startup time, per-prediction latency, memory use, and
    output quality on the target machine.
-   Use cancellation/debouncing carefully, without making the UI feel
    delayed.

### Stage C: Autocorrect

-   Add only after completion and next-word prediction are reliable.
-   Use conservative thresholds.
-   Never silently replace text by default.
-   Preserve technical vocabulary, identifiers, names, URLs, code, and
    words that the user repeatedly keeps.
-   Provide an easy way to undo or dismiss corrections.

### Stage D: Personalization

-   Learn from explicit accept/reject signals and optionally local word
    frequency.
-   Make personalization optional, visible, and resettable.
-   Avoid retaining full typed documents or sensitive text.
-   Define clear limits on what is stored and for how long.

## 7. Privacy and Security Requirements

The app handles potentially sensitive text. Privacy is a core
requirement, not a later enhancement.

-   No cloud transmission of typed text for normal prediction.
-   Do not log raw keystrokes, complete sentences, or documents.
-   Do not retain typed content by default.
-   Do not offer suggestions or collect context in password fields and
    other known sensitive fields.
-   If a field's sensitivity cannot be determined reliably, use a
    conservative policy and document the limitation.
-   Provide a global pause/disable shortcut and a visible status
    indicator.
-   Store personalization data locally and explain what it contains.
-   Provide a way to clear all locally stored personalization.
-   Avoid elevated privileges unless demonstrably required; do not
    bypass application security boundaries.
-   Protect model and dictionary loading against malformed files and
    resource exhaustion.
-   Document all external dependencies, licenses, and any network
    behavior.

## 8. Development Milestones

### Milestone 0 --- Feasibility and API research

-   Compare TSF, low-level keyboard hooks, accessibility APIs, and
    overlay approaches.
-   Create a tiny test harness to inspect text context and caret
    behavior in a controlled editor.
-   Test representative target applications.
-   Record API limitations, packaging needs, and unresolved questions.
-   Decide on the first integration path based on evidence.

**Exit criteria:** a written architecture decision and a working proof
that the chosen approach can access and modify text in at least one
controlled application.

### Milestone 1 --- Prediction engine

-   Build a standalone prediction library.
-   Load a dictionary and frequency data.
-   Implement prefix lookup and top-three ranking.
-   Add unit tests for empty input, unknown prefixes, duplicates, case
    handling, punctuation boundaries, and large dictionaries.
-   Add a command-line demonstration and benchmark.

**Exit criteria:** deterministic tests pass and dictionary lookup
latency is measured.

### Milestone 2 --- Input/context prototype

-   Detect relevant text changes in a controlled test editor.
-   Track current word, preceding context, caret changes, backspace,
    selection, punctuation, and focus.
-   Clear stale context safely.
-   Avoid collecting text outside the active eligible field.

**Exit criteria:** context tracking remains correct through a defined
test suite.

### Milestone 3 --- Popup and acceptance

-   Render three suggestions near the caret.
-   Support keyboard acceptance and dismissal.
-   Ensure the popup does not steal focus.
-   Replace or insert text correctly in the controlled editor.
-   Prevent feedback loops caused by the app's own input.

**Exit criteria:** acceptance tests show no duplicated or missing
characters in tested scenarios.

### Milestone 4 --- Contextual prediction

-   Add an n-gram baseline.
-   Evaluate compact local language models.
-   Compare top-1/top-3 quality, latency, memory, and model startup.
-   Select the simplest model that meets the quality and performance
    targets.

**Exit criteria:** measured improvement over the baseline without
unacceptable latency or resource use.

### Milestone 5 --- Compatibility expansion

-   Test browsers, Word or similar office software, VS Code, messaging
    applications, and terminals.
-   Record each application's text-context and insertion behavior.
-   Add compatibility adapters only where justified.
-   Exclude unsupported or sensitive contexts safely.

**Exit criteria:** a published compatibility matrix based on actual
tests.

### Milestone 6 --- Personalization and hardening

-   Add optional local personalization.
-   Add settings, exclusions, privacy controls, and a reliable
    pause/disable mechanism.
-   Test installation, upgrade, startup, crash recovery, and uninstall.
-   Run performance, security, and long-duration tests.

**Exit criteria:** documented release criteria are met and known
limitations are visible to users.

## 9. Initial Acceptance Criteria

These are engineering targets to validate, not guaranteed results.

-   The popup displays no more than three relevant candidates.
-   Dictionary completion target: under 30 ms for lookup on the
    development machine, measured at the prediction-service boundary.
-   End-to-end suggestion latency is measured separately, including
    context acquisition, inference, ranking, and UI update.
-   Stale prediction results never overwrite newer suggestions.
-   The popup does not steal focus during ordinary typing.
-   Accepting a candidate does not duplicate or drop characters in
    tested scenarios.
-   Backspace, punctuation, selection, focus changes, and Escape dismiss
    or update suggestions correctly.
-   Normal prediction works without a network connection.
-   Raw keystrokes and complete typed documents are not written to logs.
-   Sensitive-field behavior is tested and documented.
-   Memory use, CPU use, startup time, and model latency are
    benchmarked.
-   Compatibility claims match the tested application/version matrix.

## 10. Testing Strategy

### Unit tests

-   Dictionary parsing and malformed input.
-   Prefix lookup and ranking.
-   Case normalization and output casing.
-   Unicode and punctuation handling.
-   Context extraction.
-   Candidate deduplication and confidence thresholds.
-   Personalization opt-out and reset.
-   Stale-result cancellation.

### Integration tests

-   Type, backspace, select text, move caret, and change focus.
-   Accept suggestions at the beginning, middle, and end of a word.
-   Handle punctuation and trailing spaces.
-   Handle fast typing and rapid focus changes.
-   Ensure the popup does not consume input intended for the active
    application.
-   Verify text inserted by the assistant does not recursively trigger
    predictions.

### Compatibility matrix

Track, at minimum:

-   Application name and version.
-   Text-field type.
-   Whether context is available.
-   Whether caret location is available.
-   Whether insertion is reliable.
-   Whether the popup behaves correctly.
-   Known limitations and regression status.

### Performance evaluation

Measure: - Prediction-service latency (median and p95). - End-to-end
latency (median and p95). - Model initialization time. - Working-set
memory and CPU use. - Top-1 and top-3 prediction accuracy on an
appropriate held-out dataset. - User acceptance rate during a controlled
evaluation.

Do not use private user documents as evaluation data without explicit
permission and a privacy review.

## 11. AI-Assisted Engineering Workflow

Use a coding agent that can edit files, run builds, execute tests, and
inspect failures. Work in small, verifiable milestones.

For every milestone, ask the agent to:

1.  Read this context file and the current codebase.
2.  State the exact milestone and acceptance criteria it is
    implementing.
3.  Inspect relevant official documentation for platform-specific
    assumptions.
4.  Propose a small implementation plan before editing.
5.  Make focused changes rather than rewriting unrelated components.
6.  Build and run the relevant tests.
7.  Report commands executed, actual results, failures, and unverified
    assumptions.
8.  Update the compatibility matrix and project notes.
9.  Stop if an important API limitation or architectural decision
    requires human approval.

Do not accept claims such as "works everywhere" without a reproducible
test. Generated code is a starting point; successful compilation is not
proof of correct system-wide input behavior.

## 12. Repository Structure (Proposed)

``` text
desktop-typing-assistant/
├── context.md
├── docs/
│   ├── architecture-decisions.md
│   ├── compatibility-matrix.md
│   ├── privacy-model.md
│   └── benchmarks.md
├── app/
│   ├── shell/
│   ├── settings/
│   └── popup/
├── input/
│   ├── interfaces/
│   ├── windows-tsf/
│   ├── keyboard-prototype/
│   └── adapters/
├── prediction/
│   ├── dictionary/
│   ├── ranking/
│   ├── context/
│   └── inference/
├── personalization/
├── tests/
│   ├── unit/
│   ├── integration/
│   └── compatibility/
├── models/
│   └── README.md
└── tools/
    └── evaluation/
```

Adjust this structure after choosing the integration strategy and
implementation languages. Do not create empty folders or abstractions
without a concrete need.

## 13. Decisions Still Open

Resolve these through small experiments rather than assumptions:

-   Is TSF the primary integration path, or is a hybrid of TSF and
    application adapters necessary?
-   Which popup technology provides the best balance of non-activation,
    positioning, accessibility, and maintainability?
-   Which local model delivers a useful improvement over n-grams on the
    target hardware?
-   Which language(s) should be supported first?
-   What exact applications are required for the first release?
-   Which keyboard shortcut should accept suggestions, and how should
    conflicts be handled?
-   What is the policy for remote desktop, elevated applications, games,
    terminals, and custom editors?
-   What resource budgets are acceptable on the target PC?

## 14. Product Definition of Done

The product is not considered complete merely because it displays
suggestions.

A release candidate must have: - A documented, tested Windows
integration strategy. - Reliable behavior in explicitly supported
applications. - Useful top-three suggestions measured against a
baseline. - Responsive UI and measured resource use. - Safe text
insertion and predictable dismissal behavior. - Offline operation for
normal prediction. - Privacy controls and no raw-keystroke logging. - An
installer and clean uninstall process. - Automated tests, a
compatibility matrix, known limitations, and reproducible build
instructions.
