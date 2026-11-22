Status: round 1 resolved (auto accept: Issues 1-9 and 11-13 applied; Issue 10 escalated, still open; Issues 14-19 declined). Round 2 resolved (auto accept: R2-1 to R2-5 and R2-7 to R2-12 applied; R2-6 escalated, still open; R2-13 to R2-15 declined). Round 3 resolved (auto accept: R3-1 to R3-5 and R3-7 to R3-11 applied; R3-6 escalated, still open; R3-12 to R3-14 declined). Round 4 resolved: the user chose to apply the fixes and run round 5. R4-1 to R4-4 and R4-6 to R4-15 were applied; R4-5 is escalated and still open; R4-16 was declined. Round 5 reached the cap; the user chose to continue past it. R5-1 to R5-6 were applied, plus these Nits: the double "detail", "They read the codebase", "decide it", the Inventory labels, wireframe labelling, and sub-items judged separately. Round 6 resolved: the user chose to apply the fixes and run round 7. R6-1 to R6-4 and the proposed Nits were applied. Round 7 resolved: the user chose to apply the fixes and run round 8. R7-1, R7-3 (overruling the settled R6 Nit), R7-4 to R7-7 and the proposed Nits were applied; R7-2 is escalated and still open. Round 8 resolved: the user chose to apply the fixes and run round 9. R8-1 to R8-5 and the proposed Nits were applied; R8-6 is escalated and still open. Round 9 resolved: the user chose to apply the fixes and run round 10. R9-1 and R9-3 to R9-5 were applied. R9-2 (must) and R9-6 (each standard governs its own areas) were applied with my default choices; R9-0 is escalated and still open. Round 10 resolved: R10-1 to R10-5 were applied. The user chose to stop the loop after round 10 (accepted past the cap). Still open: R10-6 (the companion-standard link, escalated) and the recorded Nits.

# Goldfish Annotated Review Report

**Target:** `docs/ui_specification_standard.md` (full file, lines 1-96)
**Round:** 1 of up to 5
**Reviewers:** structural (completed), style (completed), comprehension (completed)
**Raw findings:** 34 (structural 7, style 15, comprehension 12). After merging duplicates: 8 Major, 11 Nit, 0 Blocker.

Round 1 is **not clean**: 8 Major findings are open.

## Prioritized Findings & AI Suggestions

### [Issue 1] [`structural`, `style`, `comprehension`] - Severity: Major
*   **Affected Scope:** [Lines 18-20](docs/ui_specification_standard.md#L18-L20), [Line 49](docs/ui_specification_standard.md#L49), [Lines 69-70](docs/ui_specification_standard.md#L69-L70) (State Coverage, Accessibility, Inventory)
*   **Raw ids:** structural-1-1, style-1-3, comprehension-1-3
*   **Discrepancy Description:** Each rule covers a different set of things:
    *   State Coverage covers "every screen and every interactive component".
    *   Accessibility covers "each screen or new component".
    *   The Inventory covers "new or changed component".
*   **Rationale:** A changed component appears to escape accessibility review, which is where regressions slip in. A reader can't tell whether the differences are deliberate.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** All three reviewers raised this on their own. The fix uses the Inventory's wording ("new or changed") in both rules. Issue 2 settles how reused components are treated.
*   ***Proposed Edit:*** The new scope wording is part of the edits for Issue 2 (State Coverage) and Issue 3 (Accessibility).

### [Issue 2] [`comprehension`] - Severity: Major
*   **Affected Scope:** [Lines 18-32](docs/ui_specification_standard.md#L18-L32) (The State Coverage Rule)
*   **Raw ids:** comprehension-1-1, comprehension-1-2
*   **Discrepancy Description:** The rule doesn't say who decides whether a state applies, or whether a state that doesn't apply must be marked N/A. It also doesn't say how detailed the coverage must be: whether every reused button needs its own states, and whether every sub-case must be covered (Error alone has five).
*   **Rationale:** Two reviewers can reach opposite verdicts on the same spec.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** This is a comprehension gap, so it can't be contested.
*   ***Proposed Edit:***
    *   Location: lines 18-20
    *   Change: replace with:
        > Every screen, and every new or changed component, must define its behavior in each state that applies to it. A reused component keeps its existing state behavior and needs no entry unless this work changes how it behaves. Within each category below, cover each listed case that can occur. Mark a case that a reader would expect but that cannot occur as "N/A", with a one-line reason. The reviewer decides whether a case is reachable. The specification fails this standard if any of the following is reachable but undefined:

### [Issue 3] [`structural`, `comprehension`] - Severity: Major
*   **Affected Scope:** [Lines 46-61](docs/ui_specification_standard.md#L46-L61) (The Accessibility Rule)
*   **Raw ids:** structural-1-2, comprehension-1-5 (part)
*   **Discrepancy Description:** The State Coverage and Reuse rules each say when a spec "fails this standard". The Accessibility Rule only says "must state", so it has no fail condition and no way to mark an item N/A.
*   **Rationale:** Accessibility is the rule most likely to be put off as a follow-up task, yet it is the only rule without a pass/fail trigger.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:***
    *   Location: lines 48-49
    *   Change: replace with:
        > Accessibility requirements are part of the specification, not a follow-up task. The specification must state the following for each screen and each new or changed component. It fails this standard if any item is missing for a screen or component in scope. Mark an item that does not apply (for example, Motion where nothing animates) as "N/A", with a one-line reason.

### [Issue 4] [`style`, `comprehension`] - Severity: Major
*   **Affected Scope:** [Lines 39-44](docs/ui_specification_standard.md#L39-L44), [Lines 94-96](docs/ui_specification_standard.md#L94-L96) (The Reuse Rule, Out of Scope)
*   **Raw ids:** style-1-4, comprehension-1-4, style-1-11, style-1-10, comprehension-1-10 (part)
*   **Discrepancy Description:** Line 43 tells reviewers to check the codebase, but line 94 says the standard "reviews the written specification only".
    *   "With paths" doesn't say what the paths point to.
    *   "Duplicates" has no threshold.
    *   "Cannot be extended" doesn't rule out using the existing item as it is.
    *   Nothing says what a reviewer without codebase access should conclude.
    *   "Hooks" is React vocabulary.
    *   The rule assumes a design system exists.
*   **Rationale:** As written, the Reuse Rule can't be applied the same way twice.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:***
    *   Location: lines 39-44
    *   Change: replace with:
        > The specification must name the existing design-system components, tokens, hooks (or the framework's equivalent shared logic), and layout primitives it uses, each with its source path in the repository. If the product has no design system, the specification says so. It fails this standard if it introduces a new component, color, spacing value, or typography style that duplicates an existing one (serves the same purpose, or differs only by a value users would not notice), unless it gives a concrete reason the existing one can neither be used as it is nor be extended. Reviewers with codebase access must verify every claim that a component "does not exist." Reviewers without codebase access record the Reuse Rule as unverified, not passed.
    *   Location: line 94
    *   Change: replace the first sentence with:
        > This standard reviews the written specification, and the code it references only to check Reuse Rule claims.

### [Issue 5] [`comprehension`, `structural`, `style`] - Severity: Major
*   **Affected Scope:** [Lines 63-65](docs/ui_specification_standard.md#L63-L65) (Requirements)
*   **Raw ids:** comprehension-1-5, structural-1-3, style-1-14
*   **Discrepancy Description:** The lead-in says "must detail", but the heading says "Non-Exhaustive Examples" and the lead-in adds "if applicable". A reader can't tell whether leaving out an area fails the spec. The list also doesn't mention the three rules, so a reviewer using it as a checklist would skip them.
*   **Rationale:** Pass/fail is unclear. The baseline standard has the same ambiguity, and this standard inherits it.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** This fixes the ambiguity here only. `feature-spec-standard.md` is out of this review's scope.
*   ***Proposed Edit:***
    *   Location: line 65
    *   Change: replace with:
        > The specification must detail each of these areas that applies to the changes. Leaving out an area that applies fails this standard. The list is non-exhaustive: an area not listed here that the work clearly needs must also be covered. The three rules above apply to every UI specification, whether or not their topic appears in this list.

### [Issue 6] [`comprehension`, `style`, `structural`] - Severity: Major
*   **Affected Scope:** [Lines 3-6](docs/ui_specification_standard.md#L3-L6) (introduction)
*   **Raw ids:** comprehension-1-6, style-1-5
*   **Discrepancy Description:** "Use it alongside" doesn't say whether a UI spec is one document that must pass both standards. The word "still" implies a history the reader doesn't have. The list of areas the other standard governs leaves out Proposed Architecture, which overlaps with State & Data.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** The path to `feature-spec-standard.md` depends on where this file will live, so that part is Issue 10 (ESCALATE).
*   ***Proposed Edit:***
    *   Location: lines 3-6
    *   Change: replace with:
        > This standard defines the quality bar for specifications of user-facing interface work (screens, flows, and components) before implementation begins. A UI specification is one document that must pass both this standard and `feature-spec-standard.md`. That standard governs the problem statement, goals, proposed architecture, alternatives, and risks; this one adds the UI rules below. Where both ask for the same thing (for example, State & Data and Proposed Architecture), one section of the specification can satisfy both.

### [Issue 7] [`style`, `comprehension`] - Severity: Major
*   **Affected Scope:** [Lines 80-82](docs/ui_specification_standard.md#L80-L82) (Copy), against [Lines 10-14](docs/ui_specification_standard.md#L10-L14) and [Lines 26-27](docs/ui_specification_standard.md#L26-L27)
*   **Raw ids:** style-1-1, comprehension-1-9
*   **Discrepancy Description:** The Copy item allows "placeholder text". The Core Principle forbids guessing at copy, and State Coverage requires "the message shown".
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:***
    *   Location: lines 80-82
    *   Change: replace with:
        > *   **Copy**: Final text for labels, buttons, empty states, and errors. Placeholder text is acceptable only when it is marked as a placeholder and states what the final text must say. State whether strings go through localization, and note right-to-left and text-expansion handling if the product is localized.

### [Issue 8] [`style`, `comprehension`] - Severity: Major
*   **Affected Scope:** [Lines 28-29](docs/ui_specification_standard.md#L28-L29) (Disabled and read-only)
*   **Raw ids:** style-1-2 (Major), comprehension-1-7 (Nit). The higher severity is kept.
*   **Discrepancy Description:** "when controls are unavailable and why the user is told they are" is hard to parse.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:***
    *   Change: replace with:
        > *   **Disabled and read-only**: which controls are unavailable under which conditions, and how the user is told why.

### [Issue 9] [`comprehension`, `style`] - Severity: Nit (merged)
*   **Affected Scope:** [Line 59](docs/ui_specification_standard.md#L59), [Lines 78-79](docs/ui_specification_standard.md#L78-L79), [Lines 51-52](docs/ui_specification_standard.md#L51-L52)
*   **Raw ids:** comprehension-1-8, style-1-13, comprehension-1-10 (part), structural-1-7
*   **Discrepancy Description:**
    *   "Unless stated otherwise" has no owner.
    *   "Target sizes" has no threshold.
    *   "Supported breakpoints" aren't defined anywhere.
    *   "Keyboard shortcuts" reads as if every screen must have them.
    *   WCAG has no link.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:***
    *   Line 51: "tab order, any keyboard shortcuts, and that every action is reachable without a pointer."
    *   Line 59: "contrast targets ([WCAG 2.2](https://www.w3.org/TR/WCAG22/) AA unless the specification states a different target and why), behavior at 200% zoom, and that no information is conveyed by color alone."
    *   Lines 78-79:
        > *   **Responsive Behavior**: Layout at each supported breakpoint (listed in the specification, or linked to where they are defined) and on touch devices, including target sizes (at least 24 by 24 CSS pixels, per WCAG 2.2 success criterion 2.5.8, unless stated otherwise) and what collapses or hides.

### [Issue 10] [`structural`, `comprehension`] - Severity: Nit
*   **Affected Scope:** the file name and location; [Line 5](docs/ui_specification_standard.md#L5)
*   **Raw ids:** structural-1-5, comprehension-1-6 (part)
*   **Discrepancy Description:** The file uses snake_case, and it lives in `C:\Goldfish\docs`. Every sibling standard is a kebab-case `*-standard.md` file in the plugin's `resources/document_standards/`. The bare reference `feature-spec-standard.md` therefore points to no file from this location.
*   ***AI Suggestion:*** ESCALATE
*   ***Justification:*** Where this standard should live is your decision, and nothing in the files tells me:
    *   in the project (and then which copy of the feature-spec standard it links to);
    *   in your own EGM plugin copy;
    *   as a contribution upstream.
    *   Also: do you want it renamed to `ui-spec-standard.md`?
*   ***Proposed Edit:*** none until you decide.

### [Issue 11] [`style`, `comprehension`] - Severity: Nit
*   **Affected Scope:** [Lines 30-32](docs/ui_specification_standard.md#L30-L32)
*   **Raw ids:** style-1-7, style-1-8, comprehension-1-11
*   **Discrepancy Description:** Missing images aren't overflow, and leaving a page mid-action isn't concurrency.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** Rename the labels to "**Overflow and missing content**" and "**Concurrency and interruption**". The bullet text stays the same.

### [Issue 12] [`style`, `comprehension`] - Severity: Nit
*   **Affected Scope:** [Lines 71-72](docs/ui_specification_standard.md#L71-L72) (Visual Reference)
*   **Raw ids:** style-1-12, comprehension-1-12
*   **Discrepancy Description:** "Precise text description" gives no test for what counts as precise.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** Replace "or a precise text description of layout where none exist" with "or, where none exist, a text description of the layout that gives each region, its order, its alignment, and its size relative to existing layout primitives".

### [Issue 13] [`style`] - Severity: Nit
*   **Affected Scope:** [Line 61](docs/ui_specification_standard.md#L61) (Motion)
*   **Raw ids:** style-1-9 (part)
*   **Discrepancy Description:** The Motion item states a requirement, but the lead-in asks for things the spec must *state*.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** "*   **Motion**: how each animation behaves when the user has asked for reduced motion."

### [Issue 14] [`style`] - Severity: Nit
*   **Raw ids:** style-1-6
*   **Discrepancy Description:** The Core Principle (lines 10-14) is a single 45-word sentence.
*   ***AI Suggestion:*** DECLINE
*   ***Justification:*** It mirrors the single-sentence test in the sibling standard and reads in one pass.

### [Issue 15] [`style`] - Severity: Nit
*   **Raw ids:** style-1-9 (rest)
*   **Discrepancy Description:** Some Accessibility items are noun phrases and others are "that ..." clauses.
*   ***AI Suggestion:*** DECLINE
*   ***Justification:*** The meaning is clear. Issue 13 fixes the one item that changed the meaning.

### [Issue 16] [`style`] - Severity: Nit
*   **Raw ids:** style-1-15
*   **Discrepancy Description:** Flows are named in scope at line 4 but not in the State Coverage Rule.
*   ***AI Suggestion:*** DECLINE
*   ***Justification:*** Each step of a flow is a screen, which State Coverage covers. User Flows (line 67) already asks for exits and back/cancel behavior.

### [Issue 17] [`structural`] - Severity: Nit
*   **Raw ids:** structural-1-4
*   **Discrepancy Description:** This standard has an Out of Scope section and no Risks & Limits section, which is the reverse of the sibling standard.
*   ***AI Suggestion:*** DECLINE
*   ***Justification:*** The reviewer itself judged this acceptable. Issue 6 makes clear that risks are governed by the feature-spec standard.

### [Issue 18] [`structural`] - Severity: Nit
*   **Raw ids:** structural-1-6
*   **Discrepancy Description:** Bullet text after the bold label is lowercase in the rule lists and capitalised in Requirements.
*   ***AI Suggestion:*** DECLINE
*   ***Justification:*** This is cosmetic. The two lists are fragments and sentences respectively.

### [Issue 19] [`style`] - Severity: Nit
*   **Raw ids:** style-1-14
*   **Discrepancy Description:** "If any" (line 87) repeats "if applicable".
*   ***AI Suggestion:*** DECLINE
*   ***Justification:*** After the Issue 5 change, "if any" says that emitting no events is a valid answer, which is a different point.

---

# Round 2

**Target:** `docs/ui_specification_standard.md` (full file, lines 1-124)
**Reviewers:** structural (completed: 0 Major, 6 Nit), style (completed: 3 Major, 7 Nit), comprehension (completed: 7 Major, 5 Nit)
**After merging duplicates:** 6 Major, 9 Nit, 0 Blocker. Round 2 is **not clean**.

**Reopened from round 1.** R2-2, R2-3 and R2-4 sit on text that round 1 added. Undoing those fixes would bring back the round 1 problems instead of solving these, so this is not churn. The round 1 fixes were not precise enough.

## Prioritized Findings & AI Suggestions

### [R2-1] [`comprehension`, `style`, `structural`] - Severity: Major
*   **Affected Scope:** [Lines 21-27](docs/ui_specification_standard.md#L21-L27), [Lines 61-64](docs/ui_specification_standard.md#L61-L64), [Lines 82-85](docs/ui_specification_standard.md#L82-L85)
*   **Raw ids:** comprehension-2-1, -2-2, -2-3, -2-10, style-2-3, structural-2-5
*   **Discrepancy Description:**
    *   "Every screen" has no limit. Adding one button to an existing screen seems to require full coverage of that screen.
    *   "Changed" means "changes how it behaves" in one place and just "changed" in another.
    *   The N/A rule differs between State Coverage ("a case a reader would expect") and Accessibility ("an item that does not apply"). A missing N/A mark has no consequence.
    *   Only State Coverage says who decides; Accessibility and Requirements don't.
    *   It isn't clear whether N/A can be applied to sub-cases (for example, the Focus events).
*   **Rationale:** Two reviewers reach different verdicts on the same spec.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** Put scope, N/A and who decides in one place, and have all three rules refer to it.
*   ***Proposed Edit:***
    *   Add a new section after the Core Principle:
        > ## Scope and Applicability
        >
        > The rules below cover the following, which this standard calls "in scope":
        >
        > *   each new screen;
        > *   on an existing screen, only the parts the work adds or changes;
        > *   each new component;
        > *   each existing component whose behavior, appearance, or copy the work changes.
        >
        > A reused component that the work does not change keeps its existing behavior and needs no entry.
        >
        > The reviewer decides whether a listed case, item, or area applies. Mark each listed case, sub-case, or item that cannot occur as "N/A", with a one-line reason. A missing N/A mark on something the reviewer agrees cannot occur is a Nit, not a failure.
    *   State Coverage (lines 21-27): replace with "Everything in scope must define its behavior in each state that applies to it. Within each category below, cover each listed case that can occur. The specification fails this standard if any of the following is reachable but undefined:"
    *   Accessibility (lines 61-64): replace the two sentences after the first with "The specification must state the following for everything in scope. It fails this standard if any item that applies is missing."

### [R2-2] [`style`] - Severity: Major (reopened from round 1, Issue 4)
*   **Affected Scope:** [Lines 51-53](docs/ui_specification_standard.md#L51-L53)
*   **Discrepancy Description:** "Differs only by a value users would not notice" can't be measured.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** Replace "(serves the same purpose, or differs only by a value users would not notice)" with "(serves the same purpose as an existing one, whatever its exact values; the reviewer decides)".

### [R2-3] [`style`, `comprehension`] - Severity: Major (reopened from round 1, Issue 5)
*   **Affected Scope:** [Lines 82-86](docs/ui_specification_standard.md#L82-L86)
*   **Raw ids:** style-2-2, comprehension-2-6, style-2-N3, structural-2-1
*   **Discrepancy Description:**
    *   "An area ... that the work clearly needs" has no test, so any reviewer can fail a spec on their own judgment.
    *   The lead-in repeats itself.
    *   "Non-exhaustive" appears twice.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** Replace lines 82-86 with:
    > The specification must detail each area below that applies, and any area not listed that a screen or behavior in the specification depends on. A reviewer who fails a specification for an unlisted area names that screen or behavior. An area can be covered in any section; it does not need its own heading. The rules above apply whether or not their topic appears in this list.

### [R2-4] [`comprehension`, `style`] - Severity: Major (reopened from round 1, Issue 4)
*   **Affected Scope:** [Lines 121-122](docs/ui_specification_standard.md#L121-L122)
*   **Raw ids:** comprehension-2-4, style-2-N6
*   **Discrepancy Description:**
    *   "Code it references" excludes the search that a "does not exist" claim needs, because such a claim references no code.
    *   The sentence can also be read two ways.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** Replace the first sentence with: "This standard reviews the written specification. Reviewers read the codebase only to check the Reuse Rule."

### [R2-5] [`comprehension`] - Severity: Major
*   **Affected Scope:** [Lines 54-56](docs/ui_specification_standard.md#L54-L56)
*   **Discrepancy Description:** The rule doesn't say whether a reviewer must look for duplicates of a new item when the spec makes no claim about it. It also doesn't say what "unverified" means for the verdict.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** "Cannot pass until verified" follows from round 1's "unverified, not passed". **Check this one**, because it sets policy. If you'd rather a spec pass with a caveat, say so.
*   ***Proposed Edit:*** Replace the last two sentences of the Reuse Rule with:
    > Reviewers with codebase access search for an existing equivalent of every new component, color, spacing value, and typography style, and verify every claim that one "does not exist." Reviewers without codebase access record the Reuse Rule as unverified. A specification cannot pass this standard until a reviewer with access has verified it.

### [R2-6] [`comprehension`, `structural`] - Severity: Major
*   **Affected Scope:** [Line 6](docs/ui_specification_standard.md#L6)
*   **Raw ids:** comprehension-2-7, structural-2-4 (part)
*   **Discrepancy Description:** `feature-spec-standard.md` has no link, and no file by that name exists next to this one.
*   ***AI Suggestion:*** ESCALATE (same as round 1, Issue 10)
*   ***Justification:*** The link depends on where you decide the file lives. This finding stays open until you decide.

### [R2-7] [`style`, `comprehension`, `structural`] - Severity: Nit
*   **Affected Scope:** [Lines 101-103](docs/ui_specification_standard.md#L101-L103)
*   **Discrepancy Description:**
    *   Stating 2.5.8 as a flat 24 px minimum ignores the criterion's exceptions.
    *   "Unless stated otherwise" needs no reason, while the contrast override does.
    *   2.5.8 has no link.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** "including target sizes (WCAG 2.2 [success criterion 2.5.8](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html): at least 24 by 24 CSS pixels, or one of its listed exceptions, unless the specification states a different target and why)".

### [R2-8] [`style`] - Severity: Nit
*   **Discrepancy Description:** "The specification says so" (line 50) and "the specification states" (line 95) use plain present tense, while every other requirement uses "must".
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** Change them to "must say so" and "must state".

### [R2-9] [`style`] - Severity: Nit
*   **Discrepancy Description:** As a verb at lines 30, 33 and 107, "State whether" reads like the noun "state".
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** Change "State whether" to "Say whether" and "State the message shown" to "Give the message shown".

### [R2-10] [`comprehension`] - Severity: Nit
*   **Discrepancy Description:** With no design system, it isn't clear whether shared logic and layout primitives must still be named (line 50).
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** "If the product has no design system, the specification must say so and still name the shared logic and layout primitives it uses."

### [R2-11] [`comprehension`] - Severity: Nit
*   **Discrepancy Description:** Telemetry's "if any" doesn't say whether "none" must be written.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** "*   **Analytics & Telemetry**: Events emitted and their names, or "none"."

### [R2-12] [`structural`] - Severity: Nit
*   **Discrepancy Description:** Line 8 runs two section names together without formatting.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** "(for example, **State & Data** here and **Proposed Architecture** there)".

### [R2-13] [`style`] - Severity: Nit
*   **Discrepancy Description:** "Not a follow-up task" (line 60) is a slogan.
*   ***AI Suggestion:*** DECLINE
*   ***Justification:*** It states the purpose of the rule in eight words.

### [R2-14] [`style`] - Severity: Nit
*   **Discrepancy Description:** The URL-persistence rationale (lines 98-99) assumes a design choice.
*   ***AI Suggestion:*** DECLINE
*   ***Justification:*** The clause asks the spec to say what is persisted and why it matters. It doesn't require persistence.

### [R2-15] [`structural`] - Severity: Nit
*   **Discrepancy Description:** Label styles are inconsistent ("and" vs "&", lowercase vs capitalised), and the Out of Scope section has no counterpart in the sibling standard.
*   ***AI Suggestion:*** DECLINE
*   ***Justification:*** Both are cosmetic. These are the same as round 1 Issues 17 and 18, which were declined, so they are settled.

---

# Round 3

**Target:** `docs/ui_specification_standard.md` (full file, lines 1-145)
**Reviewers:** structural (completed: 0 Major, 7 Nit), style (completed: 3 Major, 7 Nit), comprehension (completed: 6 Major, 6 Nit)
**After merging duplicates and setting aside settled findings:** 6 open Major, 8 Nit, 0 Blocker. Round 3 is **not clean**.

**Settled findings raised again (not reopened):**
*   URL persistence in State & Data (comprehension-3-4 as a Major, style-3-N3): this is the same scope and issue as R2-14, which was declined.
*   Label style and ampersands (structural-3-1 to 3-3): these are the same as round 1 Issues 17-18 and R2-15.

You can overrule either one if you want them changed.

**Churn check:** R3-1, R3-2 and R3-6 sit on text that round 2 added. Undoing round 2 would bring back the round 2 problems, so this is not churn. The open findings are getting narrower each round: they are now about wording, not missing rules.

## Prioritized Findings & AI Suggestions

### [R3-1] [`style`, `comprehension`, `structural`] - Severity: Major
*   **Affected Scope:** [Lines 33-36](docs/ui_specification_standard.md#L33-L36), [Lines 40-43](docs/ui_specification_standard.md#L40-L43) (Scope and Applicability, State Coverage)
*   **Raw ids:** style-3-M1, style-3-M2, style-3-N7, comprehension-3-7, comprehension-3-8, structural-3-7
*   **Discrepancy Description:**
    *   "Applies", "can occur", "cannot occur" and "reachable" are used as if they were the same test. "Reachable" also means keyboard reachability at line 85.
    *   The reviewer's list ("case, item, or area") doesn't match the N/A list ("case, sub-case, or item").
    *   The paragraph switches between talking about the reviewer and giving orders to the author.
    *   "Nit" is never defined.
    *   It isn't clear whether one N/A mark can cover several components.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:***
    *   Replace lines 33-36 with:
        > A case, item, or area *applies* if a user can encounter it as a result of this work. The reviewer decides whether something applies. The author marks each listed case, item, or area that does not apply as "N/A", with a one-line reason; one mark can cover several screens or components. A missing N/A mark on something that does not apply is not grounds for failure.
    *   Replace lines 40-43 with: "Everything in scope must define its behavior in each state below that applies to it. The specification fails this standard if any case below that applies is undefined:"

### [R3-2] [`style`, `comprehension`] - Severity: Major
*   **Affected Scope:** [Lines 63-76](docs/ui_specification_standard.md#L63-L76) (The Reuse Rule)
*   **Raw ids:** style-3-M3, comprehension-3-3, style-3-N5
*   **Discrepancy Description:**
    *   The test for a duplicate is buried in brackets, inside a 60-word sentence.
    *   The duplication test covers only "component, color, spacing value, or typography style". A new hook, layout primitive or other kind of token (radius, shadow) escapes it.
    *   The "it" in "has verified it" is unclear.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** Replace the paragraph from "It fails this standard if" to the end with:
    > A new component, token, hook, or layout primitive *duplicates* an existing one if it serves the same purpose, whatever its exact values; the reviewer decides. The specification fails this standard if it introduces a duplicate without a concrete reason the existing one can neither be used as it is nor be extended. Reviewers with codebase access search for an existing equivalent of every new component, token, hook, and layout primitive, and verify every claim that one "does not exist." Reviewers without codebase access record the Reuse Rule as unverified. A specification cannot pass this standard until a reviewer with codebase access has verified the Reuse Rule.

### [R3-3] [`comprehension`] - Severity: Major
*   **Affected Scope:** [Lines 30-31](docs/ui_specification_standard.md#L30-L31), against [Lines 63-65](docs/ui_specification_standard.md#L63-L65)
*   **Discrepancy Description:** "Needs no entry" contradicts the Reuse Rule, which says to name every existing component used, with its path.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** "A reused component that the work does not change keeps its existing behavior. It needs no State Coverage or Accessibility entry, but the Reuse Rule still applies to it."

### [R3-4] [`comprehension`] - Severity: Major
*   **Affected Scope:** [Lines 25-28](docs/ui_specification_standard.md#L25-L28)
*   **Discrepancy Description:** "Parts" of an existing screen is undefined, and it isn't clear whether a changed component is in scope as a whole. Changing one button might pull in the whole screen's landmarks and focus.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** Replace those two list items with:
    > *   on an existing screen, the controls and regions the work adds or changes, plus any screen-wide behavior they alter (for example, tab order or heading levels);
    > *   each existing component whose behavior, appearance, or copy the work changes, as a whole.

### [R3-5] [`comprehension`] - Severity: Major (reopened: R2-3, a second time)
*   **Affected Scope:** [Lines 100-103](docs/ui_specification_standard.md#L100-L103)
*   **Discrepancy Description:** "Depends on" has no test, and it isn't clear how much detail an area missing from the list needs.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Justification:*** This is the third attempt at this sentence. If round 4 raises it again, I'll treat it as churn and ask you to accept the reviewer's discretion as a deliberate judgment call.
*   ***Proposed Edit:*** Replace the first two sentences with:
    > The specification must detail each area below that applies. It must also detail any unlisted area without which a behavior it describes cannot be built (for example, offline behavior for a screen that must work offline, or print layout for a page users print), to the same level of detail. A reviewer who fails a specification for an unlisted area names both the behavior and the missing area.

### [R3-6] [`comprehension`, `structural`] - Severity: Major
*   **Affected Scope:** [Line 6](docs/ui_specification_standard.md#L6)
*   **Discrepancy Description:** `feature-spec-standard.md` has no link (the same issue as Issue 10 and R2-6).
*   ***AI Suggestion:*** ESCALATE (still open)
*   ***Justification:*** **This finding alone will keep every later round from counting as clean.** I need to know where the standard will live, or a path to link to.

### [R3-7] [`style`, `comprehension`] - Severity: Nit
*   **Affected Scope:** [Lines 140-145](docs/ui_specification_standard.md#L140-L145)
*   **Discrepancy Description:**
    *   "This standard reviews" names the wrong actor; reviewers do the reviewing.
    *   The "It" that follows is unclear.
    *   Reading code only for the Reuse Rule rules out checking whether a state applies, such as permission denied.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:***
    > Reviewers assess the written specification. They may read the codebase to check the Reuse Rule and to decide whether a case applies. This standard does not cover rendered screens, pixel accuracy, or visual taste; those require running the application and human review.

### [R3-8] [`style`] - Severity: Nit
*   **Affected Scope:** [Lines 119-125](docs/ui_specification_standard.md#L119-L125)
*   **Discrepancy Description:** A five-line bracket splits the Responsive Behavior bullet.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** "Layout at each supported breakpoint (listed in the specification, or linked to where they are defined) and on touch devices, including what collapses or hides. Target sizes meet WCAG 2.2 [success criterion 2.5.8](…) (at least 24 by 24 CSS pixels, or one of its listed exceptions) unless the specification states a different target and why."

### [R3-9] [`comprehension`] - Severity: Nit
*   **Discrepancy Description:** "Placeholder text" (line 127) can be confused with the HTML `placeholder` attribute.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** "Temporary copy is acceptable only when it is marked as temporary and states what the final text must say."

### [R3-10] [`comprehension`] - Severity: Nit
*   **Discrepancy Description:** When one section covers both standards (lines 8-10), it isn't clear whether it must meet both lists in full.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** End the sentence with "one section of the specification can satisfy both if it covers what each asks for."

### [R3-11] [`style`] - Severity: Nit
*   **Discrepancy Description:** Line 22 is wordy.
*   ***AI Suggestion:*** PROPOSE FIX
*   ***Proposed Edit:*** "The rules below apply to the following, called "in scope" here:"

### [R3-12] [`style`] - Severity: Nit
*   **Discrepancy Description:** Some instructions use the imperative ("Say whether", "Give") and others use "must".
*   ***AI Suggestion:*** DECLINE
*   ***Justification:*** The imperative is clear and was chosen in R2-9 to avoid the noun/verb clash with "State".

### [R3-13] [`comprehension`] - Severity: Nit
*   **Discrepancy Description:** "Any keyboard shortcuts" and "any bundle-size limits" don't say whether "none" must be written.
*   ***AI Suggestion:*** DECLINE
*   ***Justification:*** The N/A rule in R3-1 covers this.

### [R3-14] [`structural`] - Severity: Nit
*   **Discrepancy Description:** The scope list has no bold labels, and the Requirements lead-in is a long paragraph unlike the sibling's.
*   ***AI Suggestion:*** DECLINE
*   ***Justification:*** These are cosmetic. The content of the lead-in is needed where it is.

---

# Round 4

**Target:** `docs/ui_specification_standard.md` (full file, lines 1-150)
**Reviewers:** structural (completed: 0 Major, 6 Nit), style (completed: 1 Major, 11 Nit), comprehension (completed: 4 Major, 5 Nit)
**After merging duplicates and setting aside settled findings:** 4 open Major, 10 Nit, 0 Blocker. Round 4 is **not clean**.

**Churn detected: R4-2.** It sits on the definition of "applies" that R3-1 added, and undoing R3-1 would resolve it, but would reopen R3-1. R4-3 is close to churn: R3-1 removed the word "sub-case", and R4-3 asks for sub-cases to be spelled out again. Under the loop rules, churn stops the loop for your decision.

**Settled findings raised again (not reopened):**
*   "&" versus "and" in headings, and lowercase bullet text: round 1 Issues 17-18, R2-15 and R3-14.
*   The imperative "Say whether": R3-12.
*   The "not a follow-up task" sentence: R2-13.
*   The "Non-Exhaustive Examples" heading: R2-3 kept it to match the sibling standard.

## Open findings and proposed fixes

### [R4-1] [`style`, `comprehension`] - Severity: Major
*   **Scope:** [Lines 29-30](docs/ui_specification_standard.md#L29-L30)
*   **Discrepancy:** "Changes, as a whole" can be read as "only components changed as a whole".
*   ***AI Suggestion:*** PROPOSE FIX. "each existing component whose behavior, appearance, or copy the work changes; the whole component is in scope, not only the changed part."

### [R4-2] [`comprehension`] - Severity: Major (churn with R3-1)
*   **Scope:** [Lines 36-37](docs/ui_specification_standard.md#L36-L37)
*   **Discrepancy:** "Applies if a user can encounter it" doesn't fit areas that only engineers deal with: State & Data, Performance Budget, Telemetry and the Verification Plan. Read literally, those areas never apply.
*   ***AI Suggestion:*** PROPOSE FIX. Add a second condition rather than reverting R3-1: "A case, item, or area *applies* if a user can encounter it as a result of this work, or if an engineer must decide it to build this work."

### [R4-3] [`comprehension`, `style`] - Severity: Major (close to churn with R3-1)
*   **Scope:** [Lines 44-46](docs/ui_specification_standard.md#L44-L46), [Lines 85-86](docs/ui_specification_standard.md#L85-L86)
*   **Discrepancy:** It isn't clear whether pass/fail and N/A work per bullet (Focus) or per named case within it (focus on route change). Line 44 says "state" and line 46 says "case".
*   ***AI Suggestion:*** PROPOSE FIX. Add to Scope and Applicability: "Each case named within a bullet (for example, 'initial load' under Loading, or 'route change' under Focus) is judged separately." Change line 44 to "each case below".

### [R4-4] [`comprehension`, `style`] - Severity: Major
*   **Scope:** [Lines 127-130](docs/ui_specification_standard.md#L127-L130)
*   **Discrepancy:** The target-size sentence describes the built UI, not what the spec must state, and it sits outside the Accessibility Rule.
*   ***AI Suggestion:*** PROPOSE FIX. Move it into the Visual bullet: "contrast and target-size targets (WCAG 2.2 AA, and target sizes of at least 24 by 24 CSS pixels per [success criterion 2.5.8](https://www.w3.org/TR/WCAG22/#target-size-minimum) or one of its listed exceptions, unless the specification states a different target and why), ..." Then delete it from Responsive Behavior. This link is the normative one (structural-4-5).

### [R4-5] [`comprehension`, `structural`] - Severity: Major (fourth round)
*   **Scope:** [Line 6](docs/ui_specification_standard.md#L6)
*   **Discrepancy:** `feature-spec-standard.md` has no link.
*   ***AI Suggestion:*** ESCALATE (still open). This needs your decision on where the file lives.

### Nits I propose to fix (R4-6 to R4-15)
*   **R4-6** (style N1, N2): change lines 44 and 62 so that "the specification" is the subject, as at line 85.
*   **R4-7** (style N3, comprehension): change "The author marks" to "The author should mark", and add that "none" in Telemetry is an answer, not an N/A mark.
*   **R4-8** (style N5): change line 8 to "Where both ask for overlapping content".
*   **R4-9** (style N6): change "whatever its exact values" to "whatever its exact values or implementation".
*   **R4-10** (style N9): change the Inventory to "Each screen and new or changed component, its place in the hierarchy, and whether it is reused or new."
*   **R4-11** (style N10): rename the last heading to "Limits of This Standard" so it doesn't clash with "in scope". Change "may read the codebase to check" to "read the codebase to verify the Reuse Rule, and may read it to decide whether a case applies".
*   **R4-12** (style N11): change the closing to "those require running the application, and visual taste requires human review."
*   **R4-13** (comprehension): a reviewer without codebase access returns the verdict "incomplete".
*   **R4-14** (comprehension): change "to the same level" to "in enough detail to build that behavior".
*   **R4-15** (structural): line 33 becomes "needs no definition under the State Coverage Rule or the Accessibility Rule". Lines 7-8 name the sibling's sections exactly: goals and non-goals, risks and mitigations.

### Nits declined
*   **R4-16** (comprehension): whether "applies" also governs the feature-spec standard. Declined, because that standard's own rules govern its own areas.

---

# Round 5 (cap reached)

**Target:** `docs/ui_specification_standard.md` (full file, lines 1-159)
**Reviewers:** structural (completed: 0 Major, 5 Nit), style (completed: 2 Major, 8 Nit), comprehension (completed: 6 Major, 6 Nit)
**After merging duplicates and setting aside settled findings:** 6 open Major, about 12 Nit, 0 Blocker. Round 5 is **not clean**.

**The loop did not converge.** None of the 5 rounds was clean. Structural reported no Majors in rounds 2-5, and style's Majors fell from 4 to 2. Comprehension found 4-7 Majors every round. Most of them landed on text that the previous round's fix had added.

**Churn:** R5-1, R5-2 and R5-6 sit on text added in round 4 (R4-4, R4-13 and R4-7). Undoing those fixes would resolve them, but would bring back the round 4 problems. R5-3 and R5-4 sit on the scope text from rounds 3-4.

**The link finding is now a Nit.** Both structural and comprehension rated the unlinked `feature-spec-standard.md` a Nit this round. It is still unresolved and waiting for your decision (escalated since round 1, Issue 10).

## Open findings by severity

### Major
*   **R5-1** [`style`, `comprehension`], lines 101-107, Visual: it is unclear whether "unless the specification states a different target" covers contrast, target size or both, and whether a lower target is allowed. Listing 2.5.8 next to "AA" wrongly suggests it isn't part of AA.
    *   *Proposed fix:* "contrast and target sizes that meet WCAG 2.2 AA (target sizes per success criterion 2.5.8: at least 24 by 24 CSS pixels, or one of its listed exceptions). A specification may set a different target for either only by stating it and why."
*   **R5-2** [`style`, `comprehension`], lines 83-85: the verdict "incomplete" is undefined, and nothing says which verdict wins when rules disagree.
    *   *Proposed fix:* in Limits, "A review returns one verdict: *fail* if any rule fails, otherwise *incomplete* if the Reuse Rule is unverified, otherwise *pass*."
*   **R5-3** [`comprehension`], lines 30-31: it is unclear whether giving an existing component a new prop or label counts as "changing" it.
    *   *Proposed fix:* "changes" means changing the component's own source. A new configuration at a call site is covered by R5-4.
*   **R5-4** [`comprehension`], lines 33-35: when an unchanged reused component sits on an in-scope screen, it is unclear who defines its copy, its states there, and its place in the tab order.
    *   *Proposed fix:* "The exemption covers the component's internals. How it appears on an in-scope screen, including the copy passed to it, its states there and its place in tab order, is in scope."
*   **R5-5** [`comprehension`], lines 126-130: nothing says what a reviewer should do when they can't open linked mockups.
    *   *Proposed fix:* "Linked mockups must be openable by the reviewer; if they are not, the reviewer returns *incomplete*."
*   **R5-6** [`comprehension`], lines 147-148: "'None' is an answer" conflicts with the rule that the reviewer decides what applies.
    *   *Proposed fix:* "This area always applies; if no events are emitted, the specification states 'none'."

### Nit (main ones)
*   The link to `feature-spec-standard.md` (escalated, open).
*   A double "detail" in the Requirements lead-in (lines 113-114).
*   "They read the codebase" in Limits overstates the rule, because some reviewers have no codebase access.
*   "Decide it" (line 38) is unclear.
*   The Inventory doesn't say how to label a changed existing component.
*   It is unclear whether wireframes, as well as mockups, need normative labelling.
*   It is unclear whether design tokens are listed one by one or as a token file.
*   It is unclear whether sub-items within a Requirements area are judged separately.
*   Settled and not reopened: "&" versus "and", label casing, the imperative "Say whether", the "follow-up task" sentence, and URL persistence.

---

# Round 6 (past the cap)

**Target:** `docs/ui_specification_standard.md` (full file, lines 1-169)
**Reviewers:** structural (completed: 2 Major, 7 Nit), style (completed: 2 Major, 9 Nit), comprehension (completed: 3 Major, 8 Nit)
**After merging duplicates and setting aside settled findings:** 4 open Major, about 10 Nit, 0 Blocker. Round 6 is **not clean**.

**Churn:** all four Majors sit on text that round 5 added. R6-1 and R6-2 are defects I introduced: a misdirected pronoun, and verdict rules scattered across the document. Undoing R5-2 would remove them, but the "incomplete" verdict would become undefined again.

## Open findings and proposed fixes

### Major
*   **R6-1** [`structural`, `style`, `comprehension`], lines 162-164: "read it" grammatically refers to the written spec, not the codebase.
    *   *Proposed fix:* name "the codebase" explicitly. Say that codebase evidence may inform the Reuse Rule and decisions about whether something applies. It also counts when the codebase contradicts something the spec claims about existing behavior.
*   **R6-2** [`structural`, `style`], lines 89, 135-136, 164-166: the verdict rules are spread over three sections and sit under "Limits".
    *   *Proposed fix:* add a new `## Verdicts` section after the Accessibility Rule, holding the fail/incomplete/pass order, every trigger for "incomplete", and the codebase-access rules. The Reuse Rule and Visual Reference keep only one-line pointers to it. Limits goes back to stating what the standard does not cover.
*   **R6-3** [`comprehension`], lines 84-88: "unverified" covers the whole Reuse Rule, although missing source paths or a missing reason for a declared duplicate can be seen from the text alone.
    *   *Proposed fix:* "unverified" covers only the checks that need the codebase (searching for equivalents and confirming "does not exist" claims). Failures visible in the text fail the spec regardless of access.
*   **R6-4** [`comprehension`], lines 34-38: it is unclear whether "uses the table's default empty state" defines that case for a reused component.
    *   *Proposed fix:* "Naming the component's existing behavior for a case (for example, 'the table's built-in empty message') defines that case."

### Nits I propose to fix
*   "Different contrast or target-size target" becomes "threshold".
*   The circular "needed to build ... to build it" becomes "that a described behavior depends on".
*   Add that one definition can cover several screens or components, parallel to the N/A rule.
*   With no design system, tokens must still be named.
*   Give the two rule lead-ins the same punctuation.
*   Reflow the lines left short by earlier edits.
*   The Inventory covers only in-scope items, plus the reused components that appear on in-scope screens.

### Nits declined or settled
*   The verdict across both standards: this standard governs only its own verdict.
*   The burden of bringing a whole component into scope: this is the deliberate R4-1 decision.
*   Detail level for blanket statements in Visual: the reviewer decides.
*   Splitting the Visual bullet: cosmetic.
*   Settled and not reopened: the heading "Non-Exhaustive Examples", "&" versus "and", casing, the imperative voice, URL persistence, and section order.
*   The link to `feature-spec-standard.md`: escalated, still open (Nit).

---

# Round 7 (past the cap)

**Target:** `docs/ui_specification_standard.md` (full file, lines 1-185)
**Reviewers:** structural (completed: 0 Major, 6 Nit), style (completed: 1 Major, 9 Nit), comprehension (completed: 6 Major, 3 Nit)
**After merging duplicates:** 7 Major, 0 Blocker. One of the Majors is the escalated link and one is a settled finding. Round 7 is **not clean**.

Structural and style both passed the document. All of comprehension's Majors are new edge cases. Each one is a question about how much detail the spec needs, or a place where two verdict rules meet.

## Open findings and proposed fixes

### Major
*   **R7-1** [`style`], lines 80-81: the rule for a product with no design system leaves out "components".
    *   *Fix:* "still name the components, tokens, hooks, and layout primitives it uses". This also settles the hooks / shared-logic wording (style Nit 2).
*   **R7-2** [`comprehension`, `structural`], line 6: `feature-spec-standard.md` has no link and does not exist under C:\Goldfish.
    *   *ESCALATE:* this has been open since round 1 and needs your decision on where the file lives.
*   **R7-3** [`comprehension`, `structural`], lines 173-179: it is unclear whether the verdict also covers the feature-spec standard. This is the same scope as the R6 Nit, which was declined and is therefore *settled*. It was raised again, now as a Major.
    *   *Suggested overrule:* "This verdict covers both standards; a failure under `feature-spec-standard.md` is a *fail* here."
*   **R7-4** [`comprehension`], lines 140-141 and 177-178: "linked files must be openable" reads as a required-area failure (*fail*), but Verdicts gives *incomplete*.
    *   *Fix:* remove the sentence from Visual Reference. In Verdicts, write "if any file the specification links to cannot be opened".
*   **R7-5** [`comprehension`], lines 89-95 and 177: a spec with no new items and no "does not exist" claims makes it unclear whether the codebase checks count as made.
    *   *Fix:* add a third codebase check, "confirming that each named existing item exists at its stated path". Then every spec needs a reviewer with codebase access, which closes the "is a reused item really new?" gap too.
*   **R7-6** [`comprehension`], lines 77-79: it is unclear whether every token is listed or just the token set.
    *   *Fix:* "Tokens can be named by set (for example, the spacing scale and its source file)."
*   **R7-7** [`comprehension`], lines 111-117: it is unclear whether "meets WCAG 2.2 AA" written as a blanket statement is enough.
    *   *Fix (sets policy, please check):* "A blanket statement is enough for elements that use only existing tokens; give the values for each new color pair and each new target size." Treat 200% zoom the same way, with a blanket statement allowed unless the layout changes.

### Nits I propose to fix
*   Merge the two "can cover several" sentences.
*   Drop the repeated Reuse check sentence from Verdicts.
*   Split the Visual bullet.
*   State "no action available" explicitly where there is no next step.
*   Say that a changed token or hook brings its consumers on in-scope screens into scope.
*   Rename the heading to "UI Requirements (Non-Exhaustive Examples)" so it doesn't clash with the sibling standard's heading.
*   Move the "Analytics always applies" exception into the Requirements lead-in.

### Settled, not reopened
*   "&" versus "and", casing, the imperative voice, the "follow-up task" sentence, "visual taste", and discretionary use of the codebase (deliberate, like the other "reviewer decides" clauses).

---

# Round 8 (past the cap)

**Target:** `docs/ui_specification_standard.md` (full file, lines 1-192)
**Reviewers:** structural (completed: 1 Major, 6 Nit), style (completed: 2 Major, 9 Nit), comprehension (completed: 4 Major, 6 Nit)
**After merging duplicates:** 6 Major, 0 Blocker. Round 8 is **not clean**.

Comprehension's Major count went down again (6, 3, 6, 4 across rounds 5-8). All new Majors this round sit on text that round 7 added, so this is churn.

## Open findings and proposed fixes

### Major
*   **R8-1** [`style`, `comprehension`], lines 33-34: the scope bullet for a changed token or hook is circular ("each in-scope screen and component that uses it"). It also covers tokens and hooks but not changed components.
    *   *Fix (sets policy, please check):* "when the work changes an existing token, hook, or component, the parts of each screen that use it and whose appearance or behavior the change alters."
*   **R8-2** [`comprehension`], lines 26-28, 39-41 and 142-144: "in-scope screen" is undefined for an existing screen that is only partly changed.
    *   *Fix:* define it as "a screen with at least one in-scope part". The rules for reused components then apply only to reused components inside those parts.
*   **R8-3** [`comprehension`], lines 119-122: two existing tokens used together for the first time can be read as either "only existing tokens" or "a new color pair".
    *   *Fix:* "A color pair is new if the product does not already use that foreground and background together; give its values."
*   **R8-4** [`comprehension`], lines 92-99: nothing says what happens when a named item is not at its stated path.
    *   *Fix:* "A named item that does not exist at its stated path, or a 'does not exist' claim the search disproves, fails the Reuse Rule."
*   **R8-5** [`style`]: some requirements use "must" and others use plain present tense (lines 73, 75, 135, 175).
    *   *Fix:* use "must" for every binding requirement. Line 175 becomes a pointer to the Reuse Rule.
*   **R8-6** [`structural`], lines 6 and 180: `feature-spec-standard.md` has no link.
    *   *ESCALATE:* this has been open since round 1. Round 7 made it more pressing, because the single verdict now depends on that file.

### Nits I propose to fix
*   "it" in lines 39-41 becomes "the reused component".
*   Remove the repeated "failure visible in the text alone" phrase.
*   Change "Say whether" to "must state whether".
*   Change "whatever" to "regardless of", and fix the wording of "neither … nor".
*   Move the "one definition or N/A mark" sentence after the N/A sentences.
*   Rename the heading to "UI Requirements (Non-Exhaustive)".

### Declined or settled
*   Bullet casing, "&" versus "and", and how defined terms are marked: cosmetic and settled.
*   Anchor links for cross-references: cosmetic.
*   The boundary of "internals", the definition of "design system", the test for an acceptable threshold reason, and links that need a login: each falls under "the reviewer decides". These are Nits and are not fixed.

---

# Round 9 (past the cap)

**Target:** `docs/ui_specification_standard.md` (full file, lines 1-199)
**Reviewers:** structural (completed: 1 Major, 6 Nit), style (completed: 0 Major, 9 Nit), comprehension (completed: 6 Major, 6 Nit)
**Open Majors:** 7, with 0 Blocker. Round 9 is **not clean**.

For the first time, structural and style raised no Majors of their own; structural's only Major is the escalated link. Comprehension has raised 3-7 new Majors every round since round 1. Each one finds a new edge case in policy or applicability. None of them comes back after it is fixed, but a new one always appears.

## Open Majors and proposed fixes
*   **R9-0** [`structural`], lines 6 and 188: `feature-spec-standard.md` has no link (the same as R8-6 and R7-2).
    *   *ESCALATE:* this needs your decision on where the file lives.
*   **R9-1** [`comprehension`], lines 15-19 against Verdicts: the Core Principle and Verdicts give two different tests for passing.
    *   *Fix:* "The Core Principle states the intent; the rules and required areas below decide the verdict."
*   **R9-2** [`comprehension`], lines 182-185: checking the spec's claims about existing behavior against the codebase is optional ("may"), so two reviewers with the same access can disagree.
    *   *Policy (please check):* either make it "must" for reviewers with codebase access, or state that it is deliberately optional.
*   **R9-3** [`comprehension`], line 191: "any file" leaves out web URLs and links that need a login.
    *   *Fix:* "anything the specification links to, including web pages; a link that needs access the reviewer lacks counts as one that cannot be opened."
*   **R9-4** [`comprehension`], lines 124-130: a new component that uses only existing color pairs and existing sizes fits neither branch, and "new target size" is undefined.
    *   *Fix:* the single statement covers any element whose color pairs and target sizes the product already uses. "New" means a value the product does not already use. Give the contrast ratio for each new pair.
*   **R9-5** [`comprehension`], lines 83-85 and 96-101: components from a package have no "source path in the repository".
    *   *Fix:* "or, for a dependency, its package and import path".
*   **R9-6** [`comprehension`], lines 48-56 and 187-190: it is unclear whether the applicability and N/A rules here also govern the feature-spec standard's areas.
    *   *Policy (please check):* either they do, or that standard's own wording governs its areas and only the single verdict is shared.

## Nits
These are recorded but not proposed for fixing:
*   "item" used in two senses;
*   "&" versus "and" (settled);
*   casing (settled);
*   the "follow-up task" sentence (settled);
*   "note" changed to "describe";
*   the density of the Visual bullet;
*   "in-scope screen" is defined but used only once;
*   the boundary of "internals";
*   "design system" is undefined;
*   the test for a threshold reason;
*   URL persistence (settled);
*   the heading's drift from the sibling standard.

---

# Round 10 (past the cap)

**Target:** `docs/ui_specification_standard.md` (full file, lines 1-207)
**Reviewers:** structural (completed: 0 Major, 9 Nit), style (completed: 1 Major, 7 Nit), comprehension (completed: 6 Major, 6 Nit)
**Open Majors:** 6 after merging duplicates (the threshold override was raised by both style and comprehension), with 0 Blocker. Round 10 is **not clean**.

## Open Majors and proposed fixes
*   **R10-1** [`style`, `comprehension`], lines 132-133: the threshold override accepts any reason, may lower AA, and its scope is unclear.
    *   *Fix:* "A specification may set a stricter contrast or target-size threshold. A weaker one fails this standard unless the reviewer accepts a concrete reason for it."
*   **R10-2** [`comprehension`], lines 49-51: "applies as a result of this work" can mean either newly reachable cases or every case in an in-scope part.
    *   *Fix:* "every case that can occur in an in-scope part", plus one worked example for a changed existing screen.
*   **R10-3** [`comprehension`], lines 84-90: the Reuse Rule's "it uses" has no limit.
    *   *Fix:* "the existing ... it uses in in-scope parts". Shared logic counts only if the UI calls it directly.
*   **R10-4** [`comprehension`], lines 92-106: an undeclared duplicate found by the search has no stated outcome.
    *   *Fix:* "A duplicate the search finds fails the Reuse Rule unless the specification gives a concrete reason for it. A spec need not claim 'does not exist' for every new item."
*   **R10-5** [`comprehension`], lines 126-131: a color pair "already used" doesn't take text size or role into account.
    *   *Fix:* "already used for the same role (normal text, large text, or non-text UI)". The check falls under claims about existing behavior.
*   **R10-6** [`comprehension`], lines 6 and 192: `feature-spec-standard.md` has no link. Structural now rates this as a Nit.
    *   *ESCALATE:* this needs your decision on where the file lives.

## Nits
These are recorded and not fixed:
*   layout primitives are missing from scope bullet 5;
*   "in-scope screen" is defined but never used;
*   the boundary of "internals";
*   "primary task" is undefined;
*   "design system" is undefined;
*   URL persistence (settled);
*   a skipped behavior-claim check does not trigger *incomplete*;
*   "declared duplicate";
*   "rule or required area" in the sibling standard;
*   casing, "&" versus "and", and term markup (settled);
*   the line wrap at line 87.
