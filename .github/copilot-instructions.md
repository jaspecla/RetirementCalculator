# Copilot instructions for RetirementCalculator

This repository is a retirement calculator built with .NET Blazor. Follow these conventions when adding or changing code.

## Project goals
- Build a simple, clear, and maintainable retirement planning experience.
- Keep financial calculations accurate, explicit, and easy to verify.
- Favor readable code over clever abstractions.

## Technology expectations
- Use .NET Blazor for the UI and ASP.NET Core hosting model.
- Prefer modern C# features and nullable reference types.
- Keep dependencies minimal unless a new library clearly improves maintainability or UX.
- Use decimal for money and financial values rather than float or double.

## Architecture guidance
- Keep calculator business logic in dedicated services or classes, not inside Razor components.
- Use clear domain models such as retirement scenarios, assumptions, and projection results.
- Separate UI concerns from calculation concerns.
- Prefer small, focused components with simple props and clear responsibilities.
- Put reusable UI pieces in a Components folder and shared domain logic in a Services or Core folder.

## Coding conventions
- Follow standard C# naming conventions: PascalCase for types and methods, camelCase for locals and parameters.
- Use descriptive names for financial inputs and outputs, such as currentAge, retirementAge, annualContribution, expectedReturnRate, and projectedBalance.
- Keep methods short and composable.
- Add XML documentation only where it meaningfully improves clarity.
- Avoid magic numbers; define constants or configuration values for assumptions.

## Retirement calculator specifics
- Treat assumptions as first-class inputs and validate them explicitly.
- Preserve transparency around formulas and assumptions so users can understand projections.
- Prefer a single source of truth for projection rules and scenario defaults.
- Support common retirement planning concepts such as current savings, annual contributions, retirement age, investment growth, inflation, and withdrawal planning.
- When adding new features, keep the calculation flow easy to test independently of the UI.

## UI and UX guidance
- Make the interface intuitive and accessible.
- Prefer simple forms, clear labels, and obvious validation messaging.
- Show results in a way that is easy to scan, including formatted currency values and explanatory text.
- Keep the app responsive and avoid unnecessary client-side complexity.

## Testing expectations
- Add unit tests for financial calculations and scenario logic.
- Prefer testing the core projection logic separately from UI rendering.
- If adding Blazor components, use component tests when behavior is non-trivial.
- Keep tests focused on business rules and user-visible behavior.

## Change guidance
- When implementing a feature, prefer the smallest change that satisfies the requirement.
- Do not introduce unnecessary abstractions or frameworks.
- If the repo is still in its early stages, favor a simple structure over premature optimization.
