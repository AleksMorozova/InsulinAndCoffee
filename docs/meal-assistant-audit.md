# Meal Assistant architecture, cost and performance

Angular sends a message, conversation ID and retry ID through ApiService. The authenticated API resolves the application user from the JWT. MealAssistantService checks for a saved reply before interpreting the message, loads the owned conversation, validates tool arguments, searches owned history and builds a draft from owned saved foods. It saves the text and structured reply together. Angular renders the DTOs and revalidates the current draft through the calculator-draft endpoint before navigating. The existing calculator owns bolus calculation.

## Findings

| Severity | Finding | Complexity and benefit |
| --- | --- | --- |
| High | A single concurrent request per user limits bursts but leaves sequential usage and total spend uncapped. Manual retries can cost money when the earlier attempt did not save a reply. | Medium. A quota needs an explicit policy and durable accounting if it must work across instances. Deferred. |
| Medium | The provider previously logged total tokens only. | Low. Model, input, cached input, output, latency and estimated cost are now logged. |
| Medium | Recent draft summaries were repeated in provider context. | Low. Include only the latest draft within the six-message context window. |
| Medium | Draft construction queries exact candidates per name and falls back to a substring-boundary query on misses, then reloads resolved foods through the calculator. History materializes up to 250 candidates before ranking five, followed by a glucose query. | Medium. Profile PostgreSQL before batching or changing ranking. |
| Low | The exact welcome prompt for last-meal history required interpretation. | Low. It now bypasses the provider. |
| Low | Calculator draft preparation reads the stored conversation and reparses replies. Conversation titles are fetched again after successful sends. | Low to medium. Measure before changing client or persistence behavior. |

No critical issue was identified in this scoped review.

## Provider and cost

Development config defaults to OpenAI `gpt-4.1-mini`. Production model selection is deployment configuration. The request uses Chat Completions, one forced strict function call, no tool loop, no second completion and no automatic retry. Typical free-form requests make one provider call and the maximum per request is one. Saved retries and the exact recent-meals prompt make zero. The model receives a system instruction, up to six prior messages and the new message. Draft context contains food names, quantities and units. Full meals, insulin values, glucose readings and database IDs stay out of the generated context. The server retains up to 20 messages and the input message is limited to 1,000 characters. Output defaults to 500 tokens (clamped to 100–2,000), and the provider timeout defaults to 15 seconds (clamped to 1–60 seconds).

Configure optional prices under `MealAssistant:Pricing:<model>` using `InputUsdPerMillion`, `CachedInputUsdPerMillion` and `OutputUsdPerMillion`. The estimate uses provider token counts and logs null when input/output usage or required pricing is missing. If cached tokens or their rate are missing, estimate those input tokens at the regular rate. Estimates are monitoring data, with no enforced budget.

At $0.40 per million input, $0.10 cached input and $1.60 output, an illustrative uncached request with 200 input and 26 output tokens costs $0.0001216, or $0.1216 per 1,000 equivalent requests. A prior local request reported 226 total tokens and about 1.76 seconds end-to-end. The input/output split in this example is assumed. No new live benchmark or model switch was performed. [OpenAI GPT-4.1 mini pricing](https://developers.openai.com/api/docs/models/gpt-4.1-mini)

Provider attempt and failures, history search, draft construction and persistence durations are logged. Query counts above are code-derived. Database plans, p95 latency and a cheaper-model comparison remain unmeasured.

## Telegram

A future bot can call the same authenticated endpoints and render the structured DTOs for Telegram. It needs an explicit account-link flow, verified Telegram update handling, and a way to establish the mapped application principal, plus retry IDs based on the inbound update. The client must never choose an application UserId for authorization. Conversation data and calculation stay in the existing backend. Angular navigation state is client-specific, so the bot should use the structured draft or link to the web calculator. No Telegram service or schema change is required for these improvements.

