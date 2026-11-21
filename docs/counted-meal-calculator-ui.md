# Counted meal modal and Calculator item layout

## Causes and fixes

The Calculator used a fixed 4.4rem carb column containing both the number and the full provenance component. Its flex layout and the component's `overflow-wrap:anywhere` could squeeze long source descriptions into vertical fragments. Provenance now sits under the food name as a short non-wrapping indicator (“From history”, “Your value”, etc.). Full details remain in its title and accessible label. Numeric carb columns have useful minimum widths; container queries switch narrow lists to compact cards independently of the browser's overall width.

An explicit override equal to calculated carbs remains stored and active. Calculator shows a compact “Saved override” edit button instead of duplicating the number in a second input by default. Opening or focusing the editor preserves its value; typing an equal value does not collapse a focused input. Different overrides still show the calculated amount and editable override separately. No transfer, quantity, calculation, confirmation or save logic changed.

The Ask Past Me modal previously put each item into one auto-fit grid, including a large delete button, and scrolled the whole dialog including its actions. It now has compact bordered item cards, an item-name row with an accessible delete icon, a four-field row that becomes two columns when narrow, an Items heading with + Add item, one effective-carb summary when values match, and a total at the bottom. Changed overrides show calculated → used values. The body scrolls within the dialog; header and Save/Cancel footer remain outside that scroll area. Keyboard focus is contained, Escape closes the dialog, and focus returns to its opener. Existing cream/coffee tokens and controls are reused.

Restaurant/place and dish stay side by side on desktop. Portion/short note is clearly optional and has no required validator. API request contracts accept null, empty, omitted and whitespace-only descriptions for normal creation/update (and null in the existing create-from-meal contract). Blank descriptions normalize to empty text because the existing database column is non-nullable. Responses continue using the existing string convention; no schema migration or invented placeholder is needed. Existing nonblank descriptions are trimmed and retained.

## Selection remains unchanged

API and UI tests verify the exact example: Le Gril, Калифорния 1 portion × 60 g plus Суши 5 portions × 15 g = 135 g. Selecting only Суши returns a single row, quantity 5, effective carbs 75 g. A saved explicit 75 g override survives. The layout change does not transfer the other item or replace the selected total with 135 g.

## Verification

- Full backend suite with isolated localhost PostgreSQL enabled: **258 passed, 0 failed/skipped**. New HTTP cases cover null/empty/whitespace notes, omitted legacy notes, updates, unchanged totals/overrides, and one-item reuse. Existing insulin, confirmation and snapshot regressions passed.
- Full Angular suite: **99 passed**, including item editing/removal, optional-note save payloads, compact provenance, equal/different overrides, stable editing focus, keyboard behavior and scrolling.
- Real browser frame viewports **1440×1000, 1024×900 and 390×844** exercise media and container queries with the actual component/global styles. Assertions check dialog bounds, internal scrolling, visible footer, item overflow, carb width and a one-line source indicator. Separate content-width checks cover 760/540/340 px Calculator lists and 940/700/350 px modal widths. These are rendered geometry checks, not a physical-phone or screenshot acceptance session.
- Additional host-window runs passed all 96 then-current Angular tests at tablet size and the browser's narrow host size. Edge enforces a minimum host width (481 px reported for a requested 390 px window), so the final phone claim is based on the explicitly asserted 390 px frame viewport, not that host-window flag.
- `npm run build`: passed. Existing unrelated Meal Assistant CSS budget warning remains (4.80 kB vs 4.10 kB).
- `git diff --check`: passed; Git reports its existing LF/CRLF conversion notice for styles.css.

Karma used temporary local profiles and remained alive after reporting its completed success summary; it was stopped after observation. Disposable PostgreSQL test databases and temporary browser artifacts were cleaned up. No production connection or deployment was performed.

## Changed files

- `backend/src/InsulinAndCoffee.Application/Dtos/CommonDtos.cs`
- `backend/src/InsulinAndCoffee.Application/Services/DeliveryMealService.cs`
- `backend/tests/InsulinAndCoffee.Application.Tests/AuthIntegrationTests.cs`
- `frontend/src/app/core/api.service.ts`
- `frontend/src/app/pages/delivery-meals/delivery-meals.component.ts`
- New `frontend/src/app/pages/delivery-meals/delivery-meals.component.css`
- `frontend/src/app/pages/delivery-meals/structured-orders.component.spec.ts`
- `frontend/src/app/pages/calculator/calculator.component.ts`
- New `frontend/src/app/pages/calculator/calculator-layout.component.spec.ts`
- `frontend/src/app/shared/carb-provenance.component.ts` and its spec
- New test-only `frontend/src/app/testing/responsive-frame.ts`
- `frontend/src/styles.css` (Calculator-specific layout rules)
- This report.

Deploy the updated backend before or together with the frontend, because the new frontend may send a null portion note. No database migration or manual data repair is required. No commit or push was created.
