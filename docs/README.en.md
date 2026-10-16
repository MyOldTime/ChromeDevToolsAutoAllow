# ChromeDevToolsAutoAllow

A Windows x64 tray utility for observing Chrome's native remote-debugging consent dialog, with explicitly user-enabled temporary and persistent modes.

**Security warning:** remote debugging can expose browser sessions and page control. The dialog does not identify the requesting client reliably; this program cannot guarantee that a request comes from Chrome DevTools MCP. It is not affiliated with Google or the Chrome DevTools MCP project.

## Why this project exists

This project grew out of a practical development workflow: using Chrome DevTools MCP on a personal Windows development machine to inspect pages and debug issues in an already-open Chrome session.

In the `--autoConnect` flow for an existing session, Chrome asks the user to allow a remote-debugging connection. This is a security confirmation, not evidence of a broken MCP configuration. Repeated connection requests during development can mean repeatedly switching back to Chrome to click **Allow**; an unnoticed prompt can leave the task waiting for permission.

ChromeDevToolsAutoAllow reduces that repeated manual confirmation. It is a separate C# tray utility, not a replacement for Chrome DevTools MCP. It observes by default and only attempts to handle matching native consent dialogs after the user explicitly enables automatic mode. It does not modify Chrome or configure or start an MCP server.

The target is Chrome's native remote-debugging consent dialog, not webpage logins, cookie notices or website permission prompts. See [Chrome's explanation of connecting to an existing browser session](https://developer.chrome.com/blog/chrome-devtools-mcp-debug-your-browser-session) for context. Not every MCP connection mode or individual tool call uses this consent flow.

## What it helps with

| Situation | How this utility helps |
| --- | --- |
| A connection is waiting for someone to switch to Chrome and click Allow | While running with user-enabled automatic mode, it recognizes matching dialogs and attempts to invoke the Allow button. |
| Some sessions are short; others involve repeated connections | Choose a temporary 10-minute mode or a saved persistent preference, and stop from the tray. Persistent mode does not mean automatic startup or operation after exit. |
| It is unclear whether the dialog was detected or an attempt had an effect | Tray status and logs show results. Bounded read-only checks follow an attempt; an unconfirmed outcome stops automatic mode instead of repeatedly clicking. |

Use it only in a local development environment you control and whose debugging requests you are willing to approve. It is not a general-purpose popup clicker or a fix for MCP installation, configuration, network or protocol problems. **Matching a Chrome dialog does not authenticate its requester, and a dismissed dialog does not prove MCP connectivity.** If each requester must be reviewed, keep observe-only mode and approve manually.

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
