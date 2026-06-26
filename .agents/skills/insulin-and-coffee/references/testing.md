# Testing strategy

## Backend tests

Prioritize tests for:

- carb calculation logic
- known meal creation
- meal search/filtering
- save-to-known-meals flow
- validation errors
- Meal Memory retrieval
- LLM fallback behavior

## Integration tests

Use integration tests when behavior depends on:

- EF Core mapping
- database queries
- API route contracts
- migrations

Prefer realistic tests over mocked EF Core.

## Frontend tests

Test important UI behavior:

- loading state
- empty state
- API error state
- form validation
- rendering carb estimates and confidence

## Manual smoke test checklist

After feature work:

1. Backend starts.
2. Frontend starts.
3. Main meal list loads.
4. Known meals page loads.
5. Create/edit flow works.
6. Search/filter works.
7. LLM feature degrades gracefully if Ollama is off.
