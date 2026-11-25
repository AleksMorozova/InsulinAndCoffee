# Current UI review — 25 November 2026

Reviewed using the project-local Applying UX Laws skill and Product Design audit
workflow. This is an evidence-based review, not a redesign or a numeric UX score.

## Scope and evidence

The current Angular source was served locally and connected to the ASP.NET backend
using a newly created, isolated localhost PostgreSQL database. Screenshots below
were captured during this run, inspected, and saved without alteration. Main
responsive checks used **375×812** and **1440×900 CSS px** in the in-app browser.
Desktop-only supplementary screens are identified below.

The temporary account, two foods and saved assistant draft were synthetic. The
application's calculation API supplied the fixture's item calculations; no real
medical records were used. No meal or insulin confirmation was saved. Live LLM
inference and production services were not exercised. The initial fixture's enum
storage-format mismatch was corrected during setup; its temporary error is not
counted as an application defect.

The disposable database and local servers were removed/stopped afterward.
Application code, production data, calculation rules and settings were unchanged.
No automated unit tests were rerun because this task changed only audit artifacts.

## Findings, ordered by priority

Ev: **R/M** are the skill's evidence labels; **P** is a design principle; **W** means
a directly checked standards threshold. “Rendered” identifies observation in this
run. “Unrendered” identifies a code-only finding whose slow-network behavior was
not exercised. A standards failure is labelled Blocker under the skill's severity
scheme; it does not imply every user is unable to operate the application.

| # | Where | Rule ID / Ev | Observed vs expected | Severity | Recommended fix |
|---|---|---|---|---|---|
| 1 | Meal Assistant reply and Privacy text, 375 and 1440, light theme; screenshots 08–09 | AESTH / WCAG 1.4.3; W, rendered | `#7d6e63` text on solid `#f0dfc8`: **3.758:1**. Reply is 16 px/400; Privacy is 12 px/400. Both are normal-sized, active informational text; the 4.5:1 minimum applies, with no inactive/decorative/logo exception. | **Blocker: AA contrast** | Use a darker existing coffee/text token for these elements on cream. Scope the change to cream-backed text; the same muted token on the meal card's `#fffaf4` already measures **4.724:1**. |
| 2 | Food editor overlay, 1440; screenshots 11–12; [foods.component.ts](C:/InsulinAndCoffee/frontend/src/app/pages/foods/foods.component.ts:102) | JAKOB-1; P plus rendered keyboard evidence | Opening Add food leaves focus on the background opener. Pressing Tab moves to background Search foods while the overlay is open. No `dialog` or `role=dialog` exists in this state. | **Major** | Reuse the counted-meal modal's dialog semantics, initial focus, focus containment, Escape handling and focus restoration. Make the close control's accessible name descriptive. |
| 3 | Registration at 375; screenshot 02; [register.component.ts](C:/InsulinAndCoffee/frontend/src/app/pages/auth/register.component.ts:28) | POSTEL-4 / ACTIVE; P, rendered plus code | A seven-character password keeps Create account disabled after blur, with **no hint or error**, no `aria-describedby`, and no visible password-length requirement. The code requires 8–128 characters; username also has undisclosed length limits. | **Major** | Show concise requirements and touched/submitted field errors tied to each input. Preserve password validation/security; make the corrective action understandable rather than leaving an unexplained disabled button. |
| 4 | Meal Assistant desktop workspace; screenshot 09; [styles.css](C:/InsulinAndCoffee/frontend/src/styles.css:95), [app.component.ts](C:/InsulinAndCoffee/frontend/src/app/app.component.ts:87) | CLOAD-2; R-unconfirmed, rendered; layout diagnosis supplements the law | Empty `router-outlet` becomes an implicit grid row: **146.125 px** with the expanded draft and **186.7 px** in welcome state. In the expanded state the header ends at y=60.4, but the workspace starts at y=218.525. This consumes useful chat height. | **Minor** | Remove the routing anchor from layout participation or explicitly place the routed host in the single main grid row, scoped to Meal Assistant. Keep the composer and transcript sizing intact. |
| 5 | Add counted meal at 375; screenshot 07; [delivery-meals.component.ts](C:/InsulinAndCoffee/frontend/src/app/pages/delivery-meals/delivery-meals.component.ts:158) | MMODEL / POSTEL-4; P, rendered plus code | Labels omit units for Carbs, Recorded insulin and Pre-meal glucose. Historical glucose starts at **0**, with no optional/unknown explanation. Importantly, [save()](C:/InsulinAndCoffee/frontend/src/app/pages/delivery-meals/delivery-meals.component.ts:366) omits values ≤0: **this path does not persist the displayed zero as a measurement**. | **Minor** | State units (g, U, mmol/L), mark optional historical values, and show an empty/unknown state instead of a misleading placeholder zero while preserving the omission semantics. Do not reinterpret zero globally or alter clinical rules. |
| 6 | Food saving handler; [foods.component.ts](C:/InsulinAndCoffee/frontend/src/app/pages/foods/foods.component.ts:240) | DOHERTY-1; M, **unrendered** | Save has no in-flight guard/state, its button only checks form validity, and the subscription has no local error callback. Unlike the counted-meal editor, it does not disable or explain a pending save in this handler. Slow API behavior and duplicate submission were **not reproduced** in the browser. | **Minor, code-only** | Add a saving guard, disabled/busy state, and inline error recovery that retains the editor. Test a delayed response and failure before upgrading severity or claiming measured duplicate submissions. |
| 7 | Mobile navigation and counted-meal modal; screenshots 05, 07 | FITTS-2; W/platform guidance, rendered | Navigation links and Menu are **42 px** high; modal close is **36×36 px**; modal text inputs are **38 px** high. These pass the 24 px AA size check but miss the skill's 44 px comfortable-touch guidance. | **Minor, optional comfort improvement** | Consider 44 px hit areas on phone layouts without enlarging all visible glyphs or disrupting compact rows. This is **not** reported as an AA target-size failure. |

Contrast threshold and exceptions: [W3C SC 1.4.3](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html).
Target minimum and exceptions: [W3C SC 2.5.8](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html).

## What works

- Primary navigation keeps Calculator, History, Ask Past Me, Foods, Supplies and
  AI Assistant separate and accessible. Settings/Logout are in Account. The mobile
  menu opens into labelled navigation without horizontal page overflow.
- Sign-in failure has an alert and a clear retry path; the username stays entered
  and the password is cleared. A visible Signing in state appeared during the request;
  no precise response-feedback latency is claimed.
- The counted-meal modal traps keyboard focus: Shift+Tab from the first field reaches
  Close, then wraps to Cancel. Cancelling restores focus to Add counted meal.
- Current Meal clearly separates `1 serving` / `120 g` from `90 g carbs` / `24 g carbs`.
  The authoritative total is 114 g, and quantity inputs appear only after Edit.
- Measured Current Meal edit/remove targets are **44×44 px**, with center hit tests
  reaching the intended control. The desktop Calculator action is compact; the
  mobile action is accessible and full-width.
- At 375 px, the assistant composer is within the viewport: y=692.975–739.200 in an
  812 px viewport. Its transcript is independently scrollable; the meal card has no
  nested scrolling container. The document's scroll width remains 375 px.
- Calculator transfer preserved both synthetic foods and their quantities/carbs.
  It displayed calculations with 5.5 mmol/L and an explicit unconfirmed-default
  hint; saving remained guarded. This is UI verification, not a clinical assessment.
- History, Ask Past Me and Supplies empty states offer a concrete next action.
  Returning to the assistant offers the saved conversation; the draft is not lost.

## Checked, not applicable

- No fixed top-level navigation count was used. Six features are not a Miller/Hick
  violation, and the user's required ordering takes precedence over generic heuristics.
- Chat Send and Calculator transfer are different task groups. Their two primary
  actions are not reported as a blanket “more than one button” defect.
- Phone/postcode/card formatting rules do not apply to these captured forms.
  Password requirements must remain strict rather than being normalized/loosened.
- No checkout, paid add-ons, scarcity timers or fake progress bars are present in
  the inspected flows. The Calculator's numbered sections are not a multi-page wizard,
  so a wizard progress meter is not required.
- The current application declares a light color scheme; dark-theme conformance
  was not claimed or a new theme recommended.
- The Favorite checkbox has a larger labelled target, and auth links occur within
  running text. Small intrinsic checkbox/link boxes are not automatically treated as
  AA failures without checking the label/inline/user-agent exceptions.

## Evidence-weak notes, not counted as findings

- Result offers two options labelled Good (`Perfect` and `Good` enum values). The
  duplicate label is observed, but whether these should be distinct user-facing
  categories or deliberately unified needs a product decision.
- The modal leaves background page overflow visible. Background-scroll effects on
  a real touch device were not measured, so no scroll-lock defect is claimed.
- No production Render/Neon latency, field INP/CLS, physical phone keyboard overlap,
  screen-reader journey, or end-to-end LLM response quality was measured. A local
  viewport screenshot is not proof of full accessibility or production performance.

## Screen-by-screen record

### 1. Sign-in and registration — clear recovery; registration needs guidance

Desktop sign-in is readable and focused. Mobile failure has actionable feedback.
Registration needs the field requirements described in finding 3.

![Desktop sign-in](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/01-login-desktop.jpg)
![Mobile registration with unexplained disabled submit](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/02-registration-mobile-validation.jpg)
![Mobile sign-in recovery](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/03-login-mobile-error.jpg)

### 2. Navigation — accessible grouping and preserved feature access

Mobile, 375 px. Every required feature is reachable; active state is visible and
the center hit tests passed. Touch height is a minor comfort recommendation.

![Mobile navigation](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/05-mobile-navigation.jpg)

### 3. Calculator — coherent entry and successful draft transfer

Mobile empty state and desktop transferred state. The screenshot contains
synthetic, application-calculated values only; no dose was saved or confirmed.

![Mobile Calculator entry](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/04-calculator-mobile-empty.jpg)
![Desktop Calculator transfer](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/10-calculator-desktop-transferred.jpg)

### 4. Ask Past Me — useful empty state; clarify historical units and unknown values

Desktop empty state and mobile modal. Focus containment/cancel restoration passed.
Units/default-state clarity needs refinement; no historical record was created.

![Ask Past Me empty state](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/06-ask-past-me-desktop-empty.jpg)
![Mobile counted-meal modal](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/07-counted-meal-modal-mobile.jpg)

### 5. Meal Assistant — clear meal rows; contrast and desktop grid need fixes

Mobile and desktop, same persisted synthetic draft. Inline editing opened and
cancelled without changing it. No OpenAI request was sent.

![Mobile Current Meal](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/08-meal-assistant-mobile-draft.jpg)
![Desktop Current Meal and unused grid row](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/09-meal-assistant-desktop-draft.jpg)

### 6. Foods — useful search/filtering; modal keyboard behavior needs correction

Desktop only. After opening Add food, focus remained on the background Add food
button. Tab moved to Search foods behind the overlay, shown by the focus outline.
The editor was cancelled without saving.

![Food editor](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/11-food-editor-desktop.jpg)
![Keyboard focus behind Food editor overlay](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/12-food-editor-keyboard-focus.jpg)

### 7. History — clear empty state

Desktop only. No history was fabricated; the empty state offers Create your first meal.

![History empty state](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/13-history-desktop-empty.jpg)

### 8. Supplies — clear empty state

Desktop only. The empty shelf offers Add supply and explains the feature.

![Supplies empty state](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/14-supplies-desktop-empty.jpg)

### 9. Account / Settings — discoverable entry; save not exercised

Desktop only. Settings/Logout are in the account dropdown. Settings loaded and
showed its update timestamp. No settings values were edited or saved.

![Settings](C:/InsulinAndCoffee/docs/ux-audit-2026-11-25/15-settings-desktop.jpg)

## Suggested implementation order

1. Fix cream-backed text contrast with scoped existing tokens.
2. Reuse the counted-meal dialog behavior for the Food editor.
3. Explain registration requirements and validation beside each field.
4. Remove the empty routing-anchor grid row for Meal Assistant only.
5. Clarify counted-meal units/unknown values and add Food save feedback/recovery.
6. Consider small phone hit-area improvements without changing compact visual rows.

Validate fixes against the captured flows and widths, including keyboard focus,
backend error recovery and the existing draft/Calculator regression tests. No
business-rule, insulin formula, navigation feature removal or palette redesign is
needed to address these findings.
