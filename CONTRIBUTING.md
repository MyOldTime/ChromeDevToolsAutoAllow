# Contributing

1. Read [SECURITY.md](SECURITY.md) and the [development guide](docs/DEVELOPMENT.md).
2. Keep changes focused. Do not include personal settings, test logs, binaries, credentials or browser data.
3. Run `scripts/build.ps1` and `scripts/test.ps1 -UnitOnly` on Windows.
4. For UIA, state-machine or tray changes, run the full synthetic test suite on an interactive Windows desktop.
5. Describe the observed behavior, expected behavior, environment and verification boundaries in the pull request.

Preserve fail-closed behavior, exact target checks and one-attempt deduplication. Do not replace result validation with a claim that an API return equals permission success. Do not add real browser permission interactions to unattended CI.

Application code is C# 5 compatible and uses .NET Framework APIs. No NuGet dependencies are required. Keep the application and fixture/test entry points separate.

By contributing, you agree that your contribution is distributed under this repository's MIT license. Do not submit third-party artwork without its license and attribution.
