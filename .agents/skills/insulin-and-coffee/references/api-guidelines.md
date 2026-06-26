# API guidelines

## REST style

Use resource-oriented routes.

Examples:

- `GET /api/known-meals`
- `POST /api/meals/{mealId:guid}/save-to-known-meals`
- `POST /api/meal-memory/ask`
- `GET /api/lazy-meals`
- `POST /api/lazy-meals`

## Contracts before code

Before implementing an endpoint, define:

1. Route
2. Request DTO
3. Response DTO
4. Validation rules
5. Error responses
6. Frontend usage

## DTO naming

Use explicit names:

- `CreateKnownMealRequest`
- `CreateKnownMealFromMealRequest`
- `KnownMealDto`
- `KnownMealSectionsDto`
- `MealMemoryQueryRequest`
- `MealMemoryAnswerDto`

## Response design

For AI/memory responses, include traceability:

- answer text
- matched meals
- confidence
- warnings
- sources/references to saved meals

Do not return only free-form LLM text when structured data is useful.
