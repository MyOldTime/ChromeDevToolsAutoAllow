# ChromeDevToolsAutoAllow

A Windows x64 tray utility for observing Chrome's native remote-debugging consent dialog, with explicitly user-enabled temporary and persistent modes.

**Security warning:** remote debugging can expose browser sessions and page control. The dialog does not identify the requesting client reliably; this program cannot guarantee that a request comes from Chrome DevTools MCP. It is not affiliated with Google or the Chrome DevTools MCP project.

## Quick start

Download the Windows ZIP from this repository's Releases page, extract it into a writable folder and run `ChromeDevToolsAutoAllow.exe`. Requires .NET Framework 4.8. A clean installation is observe-only. No startup task or service is installed.

Exit an older ChromeConsentGuard instance first. The new project intentionally shares its legacy single-instance mutex to prevent concurrent operation.

## Build and test

```powershell
.\scripts\build.ps1
.\scripts\test.ps1 -UnitOnly
.\scripts\test.ps1
.\scripts\release.ps1 -IncludeSource
```

The build uses the Windows .NET Framework compiler without NuGet dependencies. The full test suite uses synthetic cross-process WinForms fixtures, never a real Chrome permission prompt. CI runs the non-interactive suite only.

First-release local checks: 72 unit checks and 128 full-suite checks (including those unit checks). A prior real-world session completed a Chrome/MCP connection; that is not a reliability guarantee or coverage of every Chrome version.

## Behavior and limits

- Event-driven WinEvent hooks, dedicated MTA UIA worker, no periodic window scan.
- Exact executable/window/dialog checks; webpage Document nodes are excluded.
- Default observe-only; temporary and persistent modes require the user's in-app confirmation.
- One attempt per UIA button identity; no mouse, keyboard, or repeated-input fallback.
- An unconfirmed result stops automatic mode and asks for manual handling.
- API completion does not prove authorization. Dialog disappearance does not prove MCP connectivity.
- Normal exit preserves a saved persistent preference. Stop clears it. File-write failures are reported.
- No elevation, Chrome configuration edits, or automatic startup. Stopping the tool does not revoke an existing debugging session.

See [security policy](../SECURITY.md), [contributing](../CONTRIBUTING.md) and [release notes](releases/v1.0.0.md). Licensed under [MIT](../LICENSE). Icons are original project artwork.
