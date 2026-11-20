# Meal Assistant

## Local development

From the repository root, set the key in the API project's User Secrets and start the Development launch profile.

```powershell
dotnet user-secrets set "MealAssistant:ApiKey" "YOUR_OPENAI_API_KEY" --project backend/src/InsulinAndCoffee.Api
dotnet run --project backend/src/InsulinAndCoffee.Api --launch-profile http
```

You can also provide the key through the backend environment variable `MealAssistant__ApiKey`. Development defaults select OpenAI and `gpt-4.1-mini`, with a 15-second timeout and a 500-token completion limit. The client accepts exactly one tool call per request. If the key is missing, the assistant returns HTTP 503 with setup instructions while the rest of the API remains available.

## Deployment

Configure these environment variables on the Render backend.

| Variable | Value |
| --- | --- |
| `MealAssistant__ApiKey` | OpenAI project API key |
| `MealAssistant__Model` | OpenAI Chat Completions model with strict function calling |
| `MealAssistant__Provider` | Optional. Defaults to `OpenAI` |
| `MealAssistant__TimeoutSeconds` | Optional. Defaults to 15 seconds |
| `MealAssistant__MaxCompletionTokens` | Optional. Defaults to 500 |

Run the normal backend deployment so EF Core applies the assistant schema before deploying the Angular build to Vercel. The existing frontend API URL is sufficient. Missing provider configuration returns HTTP 503 from the assistant endpoint.

The provider receives the user's question and a limited transcript, including matched draft food names and portions for follow-ups, with storage disabled in the API request. Saved meal records, notes, glucose, and dose values are assembled on the backend. User-entered questions can themselves contain sensitive data. Review the provider's applicable processing and retention terms before enabling the feature.

Conversation messages and structured assistant replies are stored in PostgreSQL. Existing text-only replies still open in the chat. The PersistAssistantResponses migration adds the structured reply column. The assistant retains up to ten turns per conversation and sends at most six prior messages to the provider. Use Delete chat to remove the current conversation and its messages. Other conversation rows remain until account deletion or database cleanup.

Smoke-test a recent-meal search, a portion follow-up, an account with no history, a pending meal, the original-meal link, and the calculator draft. Verify that the calculator requires current glucose and that viewing or preparing a meal does not create a record.

## Local test fixtures

With the local Compose database running and the API Development connection set to `localhost:5433/insulin_coffee`, run from the repository root:

```powershell
dotnet run --project backend/src/InsulinAndCoffee.Api --launch-profile http -- --seed-meal-assistant
```

The target account must already exist. The command inserts missing fixture IDs in a transaction and exits. The food names and meal notes carry the marker `[Meal Assistant local fixture v1]`. The suggested and confirmed insulin values are synthetic test data.
