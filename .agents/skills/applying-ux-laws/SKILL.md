---
name: applying-ux-laws
description: >-
  Use for any usability work on a web UI, even when the request is phrased as a bug, a complaint or a fix: reviewing or
  changing a header or main navigation, signup, login, checkout or onboarding flow, form fields and input validation
  (phone, email, address), pricing tables, dashboards, modals, and empty, error, loading or saving states. Use when
  users say a form rejects valid input, a save or action feels slow, the navigation is overwhelming, buttons are hard to
  tap, or people drop off a flow; when asked to rate or score a UI's UX; or when Hick, Fitts, Miller, Jakob, Postel,
  Doherty, Peak-End or the Laws of UX are named. Not for colour palettes, theming, dark mode, animation or visual
  design.
---

# Applying UX laws

Laws of UX as checks with rule IDs (e.g. FITTS-1); numbers only where a source gives one, or as a tagged house heuristic capped at minor.

## Top misreadings (read these first)
1. Miller is not a cap on menu, nav, plan or field counts; it is about chunking (group under labels).
   Capacity is about 4 chunks (Cowan); "7 +/- 2" is outdated. Give NO target count of top-level items
   in any form (not "cut to 7", "5-6" or "about 5"): say what to group and prioritise (JAKOB,
   overflow "More"); the count is the result of grouping, never the goal.
2. Hick is logarithmic and about choices shown at the same moment. Doubling options adds a
   constant step, not double the time; grouping and sensible defaults matter more than counting.
3. 44 px is WCAG 2.5.5 AAA and platform guidance. The WCAG 2.2 AA minimum (2.5.8) is 24x24 CSS px,
   with exceptions (spacing, equivalent, inline, user-agent control, essential). A 36 px target
   passes AA; a 44 px miss is minor.
4. Postel means lenient on FORMAT: accept spaces, dashes, brackets, a leading +, then normalise to
   one canonical stored form (phone: E.164, e.g. +35799123456). Never lenient on security:
   server-side validation stays, and passwords, codes and auth rules stay strict.
5. Doherty is not "make it faster". The user must see feedback within the limits: response
   within 0.1 s, keep flow under 1 s, a progress indicator after about 1 s, and a percent-done
   indicator only for waits of 10 s or more. A 3 s save gets an inline button state or an
   optimistic update, not a blocking full-screen overlay and not a percent bar.
6. Never give a numeric UX score or grade: there is no validated method behind one. Report only
   real defects; zero findings is a valid result for a clean page, and filling the
   "Checked, not applicable" line shows the work.
7. A flow ends with a designed completion state (what happened, what next), not `alert('Done')`,
   and progress persists across reload (draft saved, resume). Show the real step number and
   total; never a decorative or inflated progress bar.
8. Contested laws (Choice Overload, Zeigarnik memory claim) and house heuristics cap at minor;
   fake timers and pre-ticked paid add-ons are always flagged (GUARD-2, GUARD-3).

## Scope
Covers: option count, grouping, emphasis, target size, response time, flow ends, collisions, guardrail.
Does NOT cover (other skills): visual direction (frontend-design), motion
(design-motion-principles), full a11y audit, microcopy, page/grid layout, alignment, reading
order, RTL (better-* layout skills), ad and landing strategy (marketing-ads-mastery), tokens
(tailwind-design-system). WCAG numbers appear only where a law needs them. With a layout skill
installed, emit findings only with a rule ID and do not restate layout advice.

## Modes
Build (writing UI): load the route's q-files, then output a "UX acceptance checks" block of
3-6 rule IDs as checks at 375 and 1440 px, pasteable into a brief:
```
UX acceptance checks (375 and 1440 px)
- FITTS-1: every tap target >= 24x24 CSS px or meets a 2.5.8 exception
- POSTEL-1: phone accepts spaces/dashes/+, stored as E.164; server still validates
- PROX-1: each label sits closer to its own field than to the neighbouring one
```
Review (existing UI): render and measure when a URL or dev server exists. Target size via
`getBoundingClientRect()`; contrast from computed colours in light and dark at 375 and 1440;
INP/CLS are field metrics (75th percentile of real page loads), so a lab trace is a proxy and
is reported as such. For mis-tap complaints check hit-testing first (`elementFromPoint`,
overlapping layers, event and focus handlers), only then a bigger target. If nothing can be
rendered, review the code and tag every finding "unrendered".

## Routing (q-file list = union of the clusters of the route's rule IDs)
Clusters: q1 HICK CHOICE CHUNK CLOAD TESLER; q2 PROX SIM REGION CONNECT PRAGN; q3 VONR SELATT
SERIAL AESTH; q4 FITTS DOHERTY; q5 JAKOB POSTEL MMODEL ACTIVE; q6 GOAL PEAK ZEIG.

| Route | Rule IDs | Load |
|---|---|---|
| Form / checkout | CHUNK, POSTEL, FITTS, PROX (+ GOAL if multi-step) | q1, q5, q4, q2 (+ q6) |
| Navigation | JAKOB, HICK, CHUNK, SERIAL, FITTS | q5, q1, q3, q4 |
| Pricing / landing | CHOICE, HICK, VONR, SELATT, PEAK | q1, q3, q6 |
| Dashboard / table / list | CLOAD, PROX, SIM, SELATT | q1, q2, q3 |
| Wizard / onboarding | GOAL, ZEIG, ACTIVE, PEAK, DOHERTY | q6, q5, q4 |
| Empty / error / loading | PEAK, DOHERTY, TESLER | q6, q4, q1 |
| Modal | FITTS, VONR, JAKOB | q4, q3, q5 |

A multi-step form takes both Form/checkout and Wizard/onboarding. Tier B laws inside a loaded
q-file are also checked as one-liners. The guardrail below applies on every route.
Files: `references/q1-how-many.md` ... `q6-flow-end.md` (names as in the Load column), `sources.md`.

## Review output
| # | Where (file:line or element @width/theme) | Rule ID | Ev | Observed vs threshold | Severity | Fix |
|---|---|---|---|---|---|---|

Checked, not applicable: laws considered and skipped, each with its reason.
Evidence-weak notes: only laws considered and NOT reported because the evidence is too weak
(never also in the table). No scores or grades. Zero findings is valid.

## Severity
- blocker: the task cannot be finished, or a WCAG AA success criterion fails AFTER checking that
  criterion's listed exceptions (e.g. 2.5.8 spacing / equivalent / inline / essential).
- major: breaks a primary-source rule or measured threshold, and users can finish the task only
  with errors or rework.
- minor: deviates from a rule at low cost and the task still completes. Includes: misses of
  AAA-only or platform-only guidance (44 px, 44pt, 48dp) when WCAG AA passes; any finding that
  rests only on X-evidence or a house heuristic; deviations from non-threshold laws.
- nit: cosmetic or consistency difference with no measured effect.

## Numbers (status as in references/sources.md)
| Item | Value | Source | Status |
|---|---|---|---|
| Target size minimum, SC 2.5.8 (AA) | 24x24 CSS px; 5 exceptions | https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html | verified; exception list partial |
| Target size enhanced, SC 2.5.5 (AAA) | 44x44 CSS px | https://www.w3.org/WAI/WCAG22/Understanding/target-size-enhanced.html | verified |
| Apple control size | 44x44 pt | https://developer.apple.com/design/human-interface-guidelines/accessibility | unverified |
| Android touch target | 48x48 dp | https://support.google.com/accessibility/android/answer/7101858 | verified (not a Material page) |
| Text contrast, SC 1.4.3 (AA) | 4.5:1; large text 3:1 (18 pt or 14 pt bold) | https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html | verified |
| Non-text contrast, SC 1.4.11 (AA) | 3:1 | https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html | verified |
| NN/g response limits | 0.1 s / 1.0 s / 10 s | https://www.nngroup.com/articles/response-times-3-important-limits/ | verified |
| NN/g progress indicator | after about 1 s; percent-done for 10 s or more | https://www.nngroup.com/articles/progress-indicators/ | verified |
| Looped animation range | 2-10 s | same | unverified (fetcher note only) |
| Doherty threshold | 400 ms | https://lawsofux.com/doherty-threshold/ | verified (secondary only) |
| INP good / poor | <=200 ms / >500 ms, 75th percentile | https://web.dev/articles/inp | verified |
| CLS good / poor | <=0.1 / >0.25, 75th percentile | https://web.dev/articles/cls | verified |
| Working-memory capacity | about 4 chunks (Cowan 2001) | Europe PMC, PMID 11515286 | verified |
| Miller span | 7 +/- 2 (outdated; not a limit) | https://lawsofux.com/millers-law/ | verified (secondary) |
House heuristics (no source, max severity minor): CHUNK-1 groups of 3-4 for retyped codes;
CHUNK-2 split a form whose fields container is taller than one 375x667 viewport. CLS also
checks that skeletons match the final layout.

## When laws collide
| Collision | Resolution |
|---|---|
| HICK vs JAKOB | Conventions win: group, don't delete items users expect. |
| HICK vs FITTS | Frequent actions large and visible; the rest in overflow. |
| VONR vs SIM | One emphasised element per view; everything else consistent. |
| TESLER vs Occam | Hide complexity behind defaults; don't delete needed controls. |
| CHUNK vs DOHERTY | Each extra step is another round trip; split only where it helps. |
| POSTEL vs security | Lenient on input format only; validation and auth stay strict. |

## Guardrail (always checked; Ev L; default severity major, not capped by evidence)
- GUARD-1: progress shown matches the real remaining work.
- GUARD-2: no fake timers or scarcity (a countdown that restarts on reload is fake).
- GUARD-3: no pre-selected paid add-ons.
- GUARD-4: no confirmshaming.
- GUARD-5: cancelling is no harder than signing up.
EU anchors (B2C, partially verified, cite as "likely relevant"; sources.md section 4): UCPD
2005/29/EC blacklist point 7 (false limited-time claims); CRD 2011/83/EU (pre-ticked paid extras).

## Evidence legend (Ev)
R robust; M moderate; X contested (cap at minor); P principle, not an empirical finding;
R/M robust for part of the claim, moderate for the rest; L ethical or legal anchor (guardrail);
- no label (frame laws). ZEIG checks rest on resumption, not on a memory advantage (a meta-analysis found none).

## Frame note (never produces findings)
Occam (backs TESLER: defaults, not deletion), Flow (behind DOHERTY), Parkinson and Pareto (scoping)
and Cognitive Bias (the guardrail's umbrella) frame decisions; never cite them as findings.
