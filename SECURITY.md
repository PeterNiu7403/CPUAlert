# Security Policy

CPUAlert observes local processes and includes an optional privileged helper, so security reports are taken seriously.

## Supported versions

Security fixes are made on the latest `main` branch. Until the project publishes versioned GitHub releases, older source snapshots and locally packaged builds are not supported security branches.

## Reporting a vulnerability

Please use this repository's **Security → Report a vulnerability** flow to submit a private report. Do not open a public issue for a suspected vulnerability.

Include, when safe to do so:

- the affected commit or version;
- macOS and hardware version;
- a minimal reproduction;
- the expected and observed security boundary;
- whether the issue requires a privileged helper installation;
- any suggested mitigation.

Remove usernames, process names, signing identities, Team IDs, tokens, crash-report personal data, and benchmark environment data before attaching logs or traces.

You should receive an acknowledgement within seven days. Maintainers will validate the report, coordinate a fix and disclosure timeline, and credit the reporter unless anonymity is requested.

## Security boundaries

- CPUAlert is a local application and is not intended to expose a network service.
- The helper must accept only fixed secure-coded operations; arbitrary commands, paths, executables, and environments are out of scope by design.
- Root process termination requires fresh local authentication and a PID plus process-start-time identity check.
- The application must fail closed when helper identity, code-signing requirements, or target identity cannot be verified.
