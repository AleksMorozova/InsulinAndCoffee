# Meal Memory Agent

## Purpose

Meal Memory Agent helps the user remember previous meals, carbohydrate estimates, notes, and historical experiences.

It should answer questions like:

- “What did I usually count for sushi?”
- “Find similar meals to this chicken and buckwheat.”
- “What carbs did I use for this delivery order last time?”
- “Save this meal to known meals.”

## What the agent may do

- Search saved meals.
- Summarize previous user-entered notes.
- Compare similar meals.
- Explain uncertainty.
- Suggest likely matching known meals.
- Draft a proposed entry for user confirmation.

## What the agent must not do

- Must not invent meal history.
- Must not prescribe insulin dose.
- Must not claim medical safety.
- Must not update important data without user confirmation unless the endpoint is explicitly a save action.

## Local LLM integration

The LLM provider should be replaceable.
Common local provider: Ollama.

Suggested abstraction:

- `IMealMemoryAgent`
- `ILlmClient`
- `IMealMemoryRetriever`
- `IMealMemoryPromptBuilder`

LLM flow:

1. Receive user query.
2. Retrieve relevant meals/known meals from database.
3. Build prompt using only retrieved facts.
4. Ask local LLM.
5. Return answer plus structured sources.
6. Show uncertainty and warnings.

## Prompt requirements

The system prompt should say:

- You are Meal Memory Agent for Insulin & Coffee.
- Use only provided meal history.
- If history is missing, say that.
- Do not give medical dosing advice.
- Return structured answer where possible.

## Fallback behavior

If Ollama/local LLM is unavailable:

- show matched meals
- show saved carb values
- show notes
- show “AI summary unavailable”
