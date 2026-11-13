# Nutriora

Nutrition & Fitness desktop app for Windows.

**Status:** v0.1.0-alpha.1 scaffold in progress. The dashboard is a UI shell. Profiles, SQLite, food logging and calorie calculations are **not implemented yet**.

## Run locally

Requires .NET 10 SDK on Windows.

```powershell
dotnet run --project src/Nutriora.App/Nutriora.App.csproj
```

The Windows build workflow compiles and publishes a Windows x64 artifact. This is not yet an installer.

## Roadmap

Profiles, per-user local storage, food diary, hybrid food database, barcode lookup, recipes, exercise and meal suggestions.

Do not commit personal health data, local databases, secrets or API keys.
