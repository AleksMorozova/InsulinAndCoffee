# Example workflow: create endpoint

When adding a new API endpoint:

1. Inspect existing controllers/routes.
2. Propose route and DTOs.
3. Add service method.
4. Add validation.
5. Add persistence logic.
6. Add or update Angular service method.
7. Add UI state if endpoint is user-facing.
8. Add tests.

Example output:

- Route: `POST /api/meal-memory/ask`
- Request: `MealMemoryQueryRequest`
- Response: `MealMemoryAnswerDto`
- Errors: `400`, `503`
