# Contributing to Polr.NET

Thanks for helping! Bug reports, fixes, docs and features are all welcome.

## Getting started

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Docker (for the integration tests).
2. Fork the repository and create a branch for your change.
3. Build and test:

   ```bash
   dotnet build
   dotnet test
   ```

4. To try the app, run `docker compose up --build` or follow "Running without Docker" in the README.

## Guidelines

- **Keep it simple.** Polr.NET aims to stay small and easy to read. Prefer plain code over new abstractions,
  and discuss new dependencies in an issue first.
- **Match the surrounding code**: naming, comments and layout.
- **Keep the API backwards compatible** (see [docs/API.md](docs/API.md)). Clients rely on it.
- **Schema changes** go in a new `src/PolrNet/Data/Sql/NNN_description.sql` file. Never edit a script that has already shipped.
- **Add tests** for bug fixes and new behaviour. Pure logic goes in `RulesTests`; anything touching MySQL or HTTP goes in the integration tests.
- The build treats warnings as errors, so fix them before pushing.

## Pull requests

- Describe what changes and why. Link the issue if there is one.
- Keep pull requests focused: one fix or feature each.
- Make sure CI passes.

By contributing, you agree that your contributions are licensed under the GPL-2.0-or-later license of this project.
