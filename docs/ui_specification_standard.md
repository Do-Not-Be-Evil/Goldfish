# UI Specification Standard

This standard defines the quality bar for specifications of user-facing
interface work (screens, flows, and components) before implementation begins.
A UI specification is one document that must pass both this standard and
`feature-spec-standard.md`. That standard governs the problem statement,
goals and non-goals, proposed architecture, alternatives, and risks and
mitigations; this one adds the UI rules below. Where both ask for overlapping
content (for example, **State & Data** here and **Proposed Architecture**
there), one section of the specification can satisfy both if it covers what
each asks for.

## Core Principle

A UI specification passes if a competent front-end engineer (or fresh AI
session) who has never seen the product can build every screen and component
it describes, in every state, without guessing at behavior, layout, copy, or
accessibility, and without re-implementing a component the codebase already
provides. The Core Principle states the intent; the rules and required areas
below decide the verdict.

## Scope and Applicability

The rules below apply to the following, called "in scope" here:

*   each new screen;
*   on an existing screen, the controls and regions the work adds or changes,
    plus any screen-wide behavior they alter (for example, tab order or
    heading levels);
*   each new component;
*   each existing component whose own source the work changes (its behavior,
    appearance, or built-in copy); the whole component is in scope, not only
    the changed part;
*   when the work changes an existing token, hook, or component, the parts of
    each screen that use it and whose appearance or behavior the change
    alters.

An *in-scope screen* is a screen with at least one in-scope part.

A reused component whose source the work does not change keeps its existing
behavior. Its internals need no definition under the State Coverage Rule or
the Accessibility Rule, but the Reuse Rule still applies to it. When the
reused component sits inside an in-scope part of a screen, the following also
count as in scope: the copy and settings passed to the reused component, its
states on that screen, and its place in the screen's tab order. Naming the
component's existing behavior for a case (for example, "the table's built-in
empty message") defines that case.

A case, item, or area *applies* if a user can encounter it in an in-scope
part, whether or not this work made it newly reachable, or if an engineer
must make a decision about it to build this work. For example, adding a
filter to an existing list brings in "filtered to nothing" and also the
list's existing network failure, because both can occur in the changed
region. Each case named within a bullet (for example, "initial load" under
Loading, "route change" under Focus, or "caching and invalidation" under
State & Data) is judged separately. The reviewer decides whether something
applies. The author should mark each listed case, item, or area that does not
apply as "N/A", with a one-line reason. A missing N/A mark on something that
does not apply is not grounds for failure. One definition or N/A mark can
cover several screens or components that behave the same way.

## The State Coverage Rule

For everything in scope, the specification must define the behavior in each
case below that applies. It fails this standard if any such case is
undefined.

*   **Loading**: initial load, background refresh, and in-flight actions,
    including whether the UI uses skeletons, spinners, or optimistic updates.
*   **Empty**: no data yet, filtered to nothing, and first-run.
*   **Error**: network failure, validation failure, permission denied, not
    found, and partial failure (some items succeed, some fail).
*   **Disabled and read-only**: which controls are unavailable under which
    conditions, and how the user is told why.
*   **Overflow and missing content**: long text, many items, large numbers,
    and missing images.
*   **Concurrency and interruption**: double submission, stale data, and
    navigation away mid-action (including unsaved-changes handling).

"Show an error" is not a definition. For each case, the specification must
name what the user sees and what they can do next (for an error, the message
shown and the recovery action offered). Where the user can do nothing, the
specification must say so.

## The Reuse Rule

The specification must name the existing components, tokens, hooks (or the
framework's equivalent shared logic that the UI calls directly), and layout
primitives that its in-scope parts use, each with its source path in the
repository or, for a dependency, its package and import path. Tokens can be
named by set (for
example, the spacing scale and its source file). If the product has no design
system, the specification must say so; it must still name the components,
tokens, hooks, and layout primitives it uses.

A new component, token, hook, or layout primitive *duplicates* an existing one
if it serves the same purpose, regardless of its exact values or
implementation; the reviewer decides. The specification fails this standard
if it introduces a duplicate without a concrete reason the existing one can be
neither used as is nor extended.

Three checks require codebase access: confirming that each named existing
item exists at its stated path, searching for an existing equivalent of every
new component, token, hook, and layout primitive, and confirming every claim
that one "does not exist." Reviewers with codebase access must make all
three. A named item that does not exist at its stated path, or a "does not
exist" claim that the search disproves, fails the Reuse Rule. So does a
duplicate the search finds, unless the specification gives a concrete reason
for it. A specification does not need a "does not exist" claim for every new
item. Other failures
under this rule, such as a missing source path or a declared duplicate
without a reason, are visible in the text and fail the specification
regardless of the reviewer's access. See Verdicts for what happens when the
codebase checks cannot be made.

## The Accessibility Rule

Accessibility requirements are part of the specification, not a follow-up
task. The specification must state the following for everything in scope. It
fails this standard if any item that applies is missing.

*   **Keyboard**: tab order, any keyboard shortcuts, and that every action is
    reachable without a pointer.
*   **Focus**: where focus goes on open, close, submit, error, and route
    change; focus trapping for modals and dialogs.
*   **Semantics**: landmark structure, heading levels, accessible names for
    icon-only controls, and ARIA roles where native elements are not used.
*   **Announcements**: how status changes, errors, and async results are
    exposed to assistive technology (for example, live regions).
*   **Visual**: contrast and target sizes that meet
    [WCAG 2.2](https://www.w3.org/TR/WCAG22/) AA. For target sizes, that is
    [success criterion 2.5.8](https://www.w3.org/TR/WCAG22/#target-size-minimum):
    at least 24 by 24 CSS pixels, or one of its listed exceptions. A single
    statement that these are met is enough for any element whose color pairs
    and target sizes the product already uses. A color pair or target size is
    new if the product does not already use it (for a color pair, that
    foreground and background together for the same role: normal text, large
    text, or non-text UI); for each new color pair, the
    specification must give both colors and the contrast ratio, and for each
    new target size, its dimensions. A specification may set a
    stricter contrast or target-size threshold. A weaker one fails this
    standard unless the reviewer accepts a concrete reason for it.
*   **Zoom and color**: behavior at 200% zoom, and that no information is
    conveyed by color alone. A single statement is enough unless the layout
    changes at 200% zoom; then the specification must describe the change.
*   **Motion**: how each animation behaves when the user has asked for
    reduced motion.

## UI Requirements (Non-Exhaustive)

The specification must detail each area below that applies; Analytics &
Telemetry always applies. It must also cover any unlisted area that a
described behavior depends on (for example, offline behavior for a screen
that must work offline, or print layout for a page users print). A reviewer
who fails a specification for an unlisted area must name both the behavior
and the missing area. An area can be covered in any section; it does not need
its own heading. The rules above apply whether or not their topic appears in
this list.

*   **User Flows**: Entry points, step sequence, exits, and back/cancel
    behavior for each primary task. A diagram or numbered list is acceptable.
*   **Screen & Component Inventory**: Each screen and component in scope, and
    each reused component inside an in-scope part of a screen; its place in
    the hierarchy; and whether it is new, changed, or reused unchanged.
*   **Visual Reference**: A link to mockups or wireframes, or, where none
    exist, a text description of the layout that gives each region, its
    order, its alignment, and its size relative to existing layout
    primitives. If mockups or wireframes exist, the specification must state
    which parts are normative and which are illustrative.
*   **State & Data**: Which component owns each piece of state, where data is
    fetched, caching and invalidation, and what is persisted in the URL
    (filters, tabs, pagination) so views are shareable and survive reload.
*   **Responsive Behavior**: Layout at each supported breakpoint (listed in
    the specification, or linked to where they are defined) and on touch
    devices, including what collapses or hides. Target sizes are covered
    under the Accessibility Rule.
*   **Copy**: Final text for labels, buttons, empty states, and errors.
    Temporary copy is acceptable only when it is marked as temporary and
    states what the final text must say. The specification must state
    whether strings go through localization, and note right-to-left and
    text-expansion handling if the product is localized.
*   **Validation**: Field rules, when validation runs (on blur, on submit),
    and where messages appear.
*   **Performance Budget**: Expected data volumes, whether lists virtualize or
    paginate, and any bundle-size or render-time limits.
*   **Analytics & Telemetry**: Events emitted and their names, or "none" if
    no events are emitted.
*   **Verification Plan**: Which states get unit, component, visual-regression,
    or end-to-end tests, and which accessibility checks run automatically
    versus by hand.

## Verdicts

Reviewers assess the written specification. The Reuse Rule sets out the
codebase checks. A reviewer with codebase access must also check the
specification's claims about existing behavior against the codebase, and
fails the specification if the codebase contradicts them; they may use the
codebase to decide whether a case applies.

A review returns one verdict, which covers both this standard and
`feature-spec-standard.md`. Each standard's own wording decides which of its
areas apply; the Scope and Applicability rules here govern only this
standard.

*   *fail* if any rule or required area of either standard fails;
*   otherwise *incomplete* if the Reuse Rule's codebase checks have not been
    made, or if anything the specification links to (including web pages)
    cannot be opened; a link that needs access the reviewer lacks counts as
    one that cannot be opened;
*   otherwise *pass*.

## Limits of This Standard

This standard does not cover rendered screens, pixel accuracy, or visual
taste. Rendered screens and pixel accuracy require running the application;
visual taste requires human review.
