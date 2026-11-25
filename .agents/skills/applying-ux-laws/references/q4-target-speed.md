# Q4 Can they hit it / is it fast?

Rule IDs: FITTS, DOHERTY (frame: Flow). Tier B laws in this file: none. Numbers and status: SKILL.md "Numbers" and sources.md.

### FITTS - Fitts's Law   [evidence: R (unconfirmed)]
Claim: the time to reach a target grows with its distance and shrinks with its size, so
  controls that matter should be big enough, well apart from neighbours and near where the pointer or thumb already is.
Source: https://lawsofux.com/fittss-law/ ; Fitts 1954, J Exp Psychol 47(6):381-391 (abstract not retrievable, so the label is carried over, not re-graded).
Applies to: buttons, icon buttons, links in dense lists, tabs, menu items, close buttons, table row actions, mobile bars.
FITTS-1 (code): every interactive element has a CSS size or padding that makes the box at least 24x24 CSS px, or it matches one of the five SC 2.5.8 exceptions (Spacing, Equivalent, Inline, User Agent Control, Essential), named in the finding.
FITTS-1 (rendered, 375 and 1440 px): for each button, link, input and icon control, `getBoundingClientRect()` gives width >= 24 and height >= 24; for any that fail, state which exception applies, else report. A failure left after the exception check is a blocker (WCAG AA).
FITTS-2 (rendered, 375 px): primary touch controls measure at least 44x44 CSS px (SC 2.5.5, AAA). A miss while FITTS-1 passes is minor. Platform figures (Apple 44 pt is unverified; Android 48 dp is on a Google accessibility help page, not a Material page) are minor and cited as platform guidance.
FITTS-3 (rendered): hit-testing matches the visual box: `document.elementFromPoint()` at the centre of each control returns that control or its child, not an overlay, sticky bar or transparent layer. If it does not, fix the layering before enlarging anything.
FITTS-4 (rendered, 375 and 1440 px): the one primary action of the view is a visible, full-size control, not inside an overflow or kebab menu, and not hidden under the fold of a fixed header or keyboard.
FITTS-5 (rendered, 375 px): hit areas of neighbouring controls (from `getBoundingClientRect()`) do not overlap each other; overlap is minor unless it causes the wrong action to fire.
Does NOT apply / misreading: not a reason to make everything huge (that kills hierarchy, see VONR); 44 px is not the AA minimum, a 36 px target passes AA; inline links in running text are exempt under SC 2.5.8; a mis-tap complaint may be a layering or handler bug, not a size problem; Fitts is about reaching, not about whether the label is understood.
Conflicts: vs HICK (frequent actions large and visible, the rest in overflow); see SKILL.md collision table.

### DOHERTY - Doherty Threshold   [evidence: M (unconfirmed)]
Claim: users stay engaged when the system answers fast enough that they get visible feedback right away; the quality of that feedback matters as much as raw speed.
Source: https://lawsofux.com/doherty-threshold/ (secondary only); Doherty & Thadani 1982, IBM Systems Journal (primary page unreachable). Response limits: https://www.nngroup.com/articles/response-times-3-important-limits/ and https://www.nngroup.com/articles/progress-indicators/.
Applies to: form submit, save, search, filters, page and route changes, uploads, empty/loading/saving states.
DOHERTY-1 (code): each async handler (submit, save, fetch on click) changes visible state (disabled, busy attribute or label change) before its first await, and restores it in both the success and error path.
DOHERTY-1 (rendered): click the action with the network throttled; the first visible change appears within 0.1 s (NN/g instant limit; Doherty's secondary figure is 400 ms). Measure with `performance.now()` between the click and the first DOM or style change. No change at all on a wait over 1 s is major if the user can double-submit or loses their place, else minor.
DOHERTY-2 (rendered): an action that takes longer than about 1 s shows a progress indicator (NN/g). Check by delaying the response by 3 s and looking for it.
DOHERTY-3 (rendered): a percent-done bar appears only for waits of 10 s or more; a short wait (a 3 s save) gets an inline button state or an optimistic update, not a blocking full-screen overlay and not a percent bar. A blocking overlay or percent bar on a short wait is minor.
DOHERTY-4 (rendered, 375 and 1440 px): a skeleton or placeholder has the same box as the final content; compare the skeleton and loaded `getBoundingClientRect()`. A lab trace of CLS and INP is a proxy; the thresholds (CLS <= 0.1, INP <= 200 ms, poor above 0.25 and 500 ms) apply to the 75th percentile of real page loads, so report the trace as such.
Does NOT apply / misreading: not "make everything faster": a slow job with honest feedback passes; 400 ms is a feedback target from a secondary page, not a proven universal limit; fake delays or fake progress are a guardrail matter (GUARD-1); background work the user never waits on needs no indicator.
Conflicts: vs CHUNK (each extra step is another round trip; split only where it helps); see SKILL.md collision table.

Frame law: Flow (behind DOHERTY) - context for why waits break concentration; never cite it as a finding.
