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
