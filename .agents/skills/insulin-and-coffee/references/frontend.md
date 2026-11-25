# Angular frontend rules

## Structure

Prefer feature-based folders, for example:

- `features/meals`
- `features/known-meals`
- `features/meal-memory`
- `features/lazy-meals`
- `core/api`
- `shared/components`

Match the actual project structure if it already exists.

## Components

- Prefer standalone components if the project uses standalone Angular.
- Keep components focused.
- Move HTTP calls to services.
- Use typed interfaces for API contracts.
- Add loading, empty, and error states.
- Avoid hiding uncertainty in food/carb UI.

## UX tone

The app tone may be warm and human, but safety-sensitive parts must stay clear.

Good labels:

- “Estimated carbs”
- “Based on your saved meals”
- “Similar meals”
- “Confidence”
- “Check manually”

Avoid:

- “Correct dose”
- “Safe to inject”
- “Guaranteed”

## API integration

- Keep API base URLs in environment/config if the project already does this.
- Keep interfaces synchronized with backend DTOs.
- Handle null/empty values explicitly.
- Show useful fallback when backend or LLM is unavailable.

## Project visual identity and UX checks

For usability work, read the project-local [Applying UX Laws skill](../../applying-ux-laws/SKILL.md) and only the reference clusters relevant to the changed flow. Upstream: https://github.com/thegreatsao/applying-ux-laws at commit `1b398e655df210b2318d1aab5a57ac45c3a16fce` (MIT). It supplies usability checks, not a colour palette or a visual theme.

For styling, use the existing application as the visual reference:

- Reuse `frontend/src/styles.css`: warm beige backgrounds (`--bg`, `--surface`, `--cream`), brown actions (`--coffee`, `--coffee-dark`), existing teal accents, border and muted-text tokens, Inter/system typography, and existing rounded controls. Improve contrast within this identity when measurements require it.
- Keep compact, readable meal rows. Separate quantity/unit from explicitly labelled carbohydrate values. Use existing primary, subtle and danger button variants, with emphasis appropriate to each task group; chat Send and Calculator transfer are separate legitimate actions.
- Preserve the primary navigation order: Calculator, History, Ask Past Me, Foods, Supplies, AI Assistant. Ask Past Me and AI Assistant remain separate. Keep Settings and Logout in the account menu; use responsive navigation rather than removing features or forcing horizontal scroll.
- Preserve routes, source provenance, empty/error states and meal-to-Calculator data. Layout or styling work does not authorize changing calculation rules, confirmation state, authentication or database contents.

Apply usability principles within this application's safety boundaries:

- A formatting-tolerance rule does not authorize guessing food units, merging distinct sources, changing numeric meaning, loosening authentication, or inventing glucose/insulin defaults.
- Pending/saving feedback must reflect actual server state. Do not optimistically present medical-adjacent mutations as committed, fabricate progress, silently replace entered values, or represent default glucose as measured.
- Group secondary metadata and uncommon controls without hiding uncertainty or required actions. Use inline busy states and preserve input on genuine failures. Keep retry/version safeguards intact.
- Preserve user-defined ordering and workflows when a general UX heuristic suggests otherwise. Avoid forcing one primary action across unrelated task groups or interpreting a law as a fixed navigation-item limit.

Verify the affected flow at 375 and 1440 CSS px; include 320/390, tablet and wide desktop widths when the layout change warrants them. Measure actual target geometry, overlap/hit testing, focus, contrast, scrolling and composer/modal visibility. Treat 24 px with applicable exceptions as the upstream WCAG AA check and 44 px as comfortable touch guidance, not an AA requirement. Mark code-only findings unrendered, distinguish field performance from lab proxies, and report concrete findings without invented UX scores. Run the relevant Angular tests/build for UI changes.
