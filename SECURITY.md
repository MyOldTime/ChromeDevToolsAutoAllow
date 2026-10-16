# Security policy and boundaries

## Supported version

The current project release line is **1.0.x**. Earlier ChromeConsentGuard builds are local development predecessors, not releases of this repository.

## Important risks

- Remote debugging can expose browser sessions, cookies and page control. Treat enabling automatic approval as granting broad browser access, not as a harmless notification preference.
- The consent dialog does not establish a verifiable requesting-client identity. The application cannot prove that a request originated from Chrome DevTools MCP or distinguish another local requester with the same dialog.
- Matching the executable path, window class, text and buttons reduces accidental matches; it is **not** a security boundary against a compromised host.
- Saved permanent mode restores on normal restart. Normal exit does not clear it. Use the stop command to clear the saved preference.
- Unknown invocation results cause a safety stop. A persistence failure is reported; an old saved setting might still restore after restart until corrected.
- Stopping this program does not revoke an existing Chrome debugging connection.

## Implementation constraints

Default observe-only; no browser configuration changes, startup task, injected DLL, elevation, network listener, coordinate fallback or keyboard fallback. Page Document nodes are excluded. Same-button attempts are not repeated automatically. The application does not claim that a closed dialog proves an MCP connection.

## Reporting a vulnerability

Use GitHub's private vulnerability reporting on the repository **if the maintainer has enabled it**. If no private route is available, open an issue asking for a private contact without including exploit details or sensitive data.

Never publish personal `settings.ini`, full browser session data, cookies, tokens, page URLs, or unredacted logs. Supply a minimal synthetic reproduction whenever possible.
