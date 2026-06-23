# Insulin & Coffee

Full-stack MVP for a single person with Type 1 Diabetes to track meals, carbohydrates, glucose readings, insulin doses, food items, and current diabetes settings.

This application is not a medical device. All insulin calculations are informational only and must be confirmed by the user.

## Stack

- Angular 18 standalone components
- ASP.NET Core Web API on .NET 9
- Entity Framework Core
- PostgreSQL

## Project Layout

- `backend/src/InsulinAndCoffee.Api` - Web API controllers, Swagger, CORS
- `backend/src/InsulinAndCoffee.Application` - DTOs, validation, meal calculation services
- `backend/src/InsulinAndCoffee.Domain` - entities and enums
- `backend/src/InsulinAndCoffee.Infrastructure` - EF Core DbContext, PostgreSQL configuration, seed data
- `frontend` - Angular application

## Features

- Dashboard with today's carbs, confirmed insulin, last meal, and quick actions
- Current calculator for food-item based meals and already-counted direct carb entries
- Meal history and meal details
- Food library
- Settings
- Already Counted: a personal "Ask Past Me" knowledge base for repeated restaurant, cafe, and delivery meals

## Seed Data

The database seeds one default user:

- Name: Aleksandra
- Email: `aleksandra@example.com`

Default settings:

- Target glucose: `6.5`
- Carb ratio: `10`
- Correction factor: `3`
- Insulin duration hours: `4`

The food library is seeded with Philadelphia Roll, Sushi Rice, Bread, Butter, Cottage Cheese Casserole, Borscht, Chicken Cutlet, Latte, and Chocolate.

## Backend Setup

1. Start PostgreSQL:

   ```powershell
   cd backend
   docker compose up -d
   ```

2. Restore and apply migrations:

   ```powershell
   cd ..
   dotnet restore
   dotnet ef database update --project backend/src/InsulinAndCoffee.Infrastructure --startup-project backend/src/InsulinAndCoffee.Api
   ```

3. Run the API:

   ```powershell
   dotnet run --project backend/src/InsulinAndCoffee.Api --launch-profile https
   ```

Swagger is available at `https://localhost:7205/swagger` when using the HTTPS launch profile.

From this workspace path, the helper script is also useful because it avoids nested shell quoting around `Insulin & Coffee`:

```powershell
.\scripts\run-api.ps1
```

## Frontend Setup

Angular CLI 18 expects an LTS Node version. If your local Node is newer and Angular warns, use Node 20 or 22 LTS.

```powershell
cd frontend
npm install --ignore-scripts
npm start
```

The install command disables npm lifecycle scripts to avoid a Windows path parsing issue in an optional native package when the workspace path contains `&`. The npm scripts call Angular through `node ./node_modules/@angular/cli/bin/ng.js` for the same reason. The compose database is exposed on local port `55432` to avoid conflicts with existing PostgreSQL services. The app runs at `http://localhost:4200` and calls the API at `http://localhost:5246/api` by default.

You can also run the frontend with:

```powershell
.\scripts\run-frontend.ps1
```

## Main Calculation Rules

- Item carbs: `weightGrams * carbsPer100g / 100`
- Total carbs: sum of item carbs
- Meal bolus: `totalCarbs / carbRatio`
- Correction bolus: `(preMealGlucose - targetGlucose) / correctionFactor` when glucose is above target
- Suggested bolus: meal bolus plus correction bolus
- Confirmed bolus: manually entered by the user and may differ from the suggestion

Saved meals store food name and carb snapshots so history remains stable after food edits.

## Already Counted

Use the `Already Counted` navigation item to manage known restaurant/cafe/delivery meals. You can:

- Search by place, dish, or tags
- Mark meals as favorites
- See favorites, most used, and recently used
- Save a meal from Meal Details into Already Counted
- Use Again to prefill the calculator with known carbs, usual insulin units, and notes

If you already created the database before this feature was added, start Docker Desktop and run:

```powershell
dotnet ef database update --project backend/src/InsulinAndCoffee.Infrastructure --startup-project backend/src/InsulinAndCoffee.Api
```

The latest migration includes the `KnownMeals` updates.
