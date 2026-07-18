---
name: insulin-and-coffee
description: Work inside the Insulin & Coffee project: .NET backend, Angular frontend, meal history, carb estimation, known meals, delivery/lazy meals, local LLM Meal Memory Agent, safe diabetes-support UX, API contracts, EF Core, tests, and project conventions.
---

# Insulin & Coffee Skill

Use this skill whenever the task is about the Insulin & Coffee application, including backend, frontend, database, API naming, meal memory, carbohydrate history, local LLM/Ollama integration, or diabetes-support user experience.

## Core product intent

Insulin & Coffee is a personal diabetes-support app. It helps the user remember meals, carbohydrate estimates, personal notes, previous reactions, known meals, lazy delivery meals, and patterns over time.

It is not a medical authority. It should support traceable memory and careful estimation, not prescribe treatment.

## Safety rules

Always follow these rules:

1. Do not present insulin dosing as medical advice.
2. Do not invent glucose reactions, carb values, meal history, or medical context.
3. If data is missing, say so and design the feature to show uncertainty.
4. Keep calculations traceable: source, amount, unit, confidence, user override.
5. Prefer user-confirmed values over LLM-generated estimates.
6. The LLM may summarize and retrieve memory; business logic must stay deterministic.
7. For medical-adjacent UI copy, use careful phrasing: “estimated”, “based on your saved history”, “check manually”.

## Project working style

Before making changes:

1. Inspect the existing repository structure.
2. Identify backend, frontend, database, and test projects.
3. Match existing naming and folder style.
4. Produce a short implementation plan.
5. Then make focused changes.

When the repo structure differs from this skill, prefer the actual repo.

## Important references

Read these files when relevant:

- `references/architecture.md` for system boundaries.
- `references/backend.md` for .NET rules.
- `references/frontend.md` for Angular rules.
- `references/api-guidelines.md` for REST/API conventions.
- `references/database.md` for EF Core and migrations.
- `references/meal-memory-agent.md` for local LLM and retrieval.
- `references/testing.md` for test strategy.
- `references/naming.md` for naming conventions.
- `references/safety.md` for diabetes-support safety wording.

## Default implementation checklist

For each feature:

1. Define the user scenario.
2. Define API contract before code.
3. Add/update backend models, DTOs, service methods, and persistence.
4. Add/update Angular models, service, route/component, loading/error/empty states.
5. Add validation and meaningful errors.
6. Add tests for core logic.
7. Update seed data or migration if needed.
8. Summarize what changed and how to run it.

## Output format for Codex responses

Prefer:

- What I changed
- Files changed
- How to run/test
- Risks/notes
- Next useful step
