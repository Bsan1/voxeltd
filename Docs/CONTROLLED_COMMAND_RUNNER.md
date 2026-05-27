# Controlled Command Runner

## Purpose

The Controlled Command Runner is an advanced/experimental execution surface. It is not the primary V1.2 setup path, is not a terminal, and does not accept freeform user-entered commands.

## Allowed Command Policy

Only commands defined in the application allowlist can appear in the runner. The renderer sends a command id, not shell text, and the Electron main process resolves that id against the allowlist before execution.

Enabled commands:

- `git --version`
- `node --version`
- `npm --version`
- `codex --version`
- `openclaw.cmd --version`
- `openclaw.cmd gateway status`
- `openclaw.cmd unity status`
- `ffmpeg -version`
- `npm.cmd install -g openclaw@latest`
- `openclaw.cmd onboard`
- `openclaw.cmd onboard --install-daemon`

## Intentionally Disabled Until Later

The runner lists these setup commands as future work, but they are not executable in V1.2:

- `winget install --id Gyan.FFmpeg -e`
- `winget install --id OBSProject.OBSStudio -e`

## Blocked Arbitrary Command Policy

- No freeform command input exists.
- User-provided command text is not accepted.
- Dangerous commands are not exposed as runnable actions.
- Secrets and tokens must never be pasted into commands.
- This runner must not be used to bypass manual approval.

## Confirmation Requirement

Every enabled command requires an explicit confirmation panel before execution. The panel shows:

- command label
- exact command and args
- risk level
- what it does

OpenClaw install and onboarding commands also warn that:

- global npm state may change
- onboarding may create local OpenClaw config/state
- daemon onboarding may configure a background service
- onboarding can require terminal-style interaction that this runner cannot provide cleanly
- no tokens should be pasted into this app
- provider or channel secrets should be entered only into the actual OpenClaw process/tool

`openclaw.cmd --version` means CLI detected only. It does not verify `openclaw.cmd onboard` completion. Basic onboarding may need a real terminal, and daemon onboarding is optional/advanced.

Default recommendation: run `openclaw.cmd onboard` first. Use `openclaw.cmd onboard --install-daemon` only for always-on remote/background behavior on your own machine.

Future System Control Center work may bring OpenClaw Gateway, Telegram channel, OpenClaw Unity Plugin connection, Unity Editor readiness, recorder tools, and model/provider roles online from one local control panel. That is future architecture only; this generated workflow does not implement service orchestration or token storage.

## Risk Levels

- `safe`: enabled read-only checks
- `setup`: allowlisted OpenClaw setup actions are advanced/experimental and not in the primary setup path
- `dangerous`: not runnable

## Timeout And Logging Behavior

Enabled commands use bounded timeouts and return structured results. Only one controlled command can run at a time. The UI shows:

- running state
- passed / failed / timed-out / cancelled status
- exact command metadata, start time, end time, exit code, and timeout
- stdout
- stderr
- error message when the process cannot start or exits unsuccessfully

Very long output is truncated before it reaches the renderer.

The active command can be cancelled from the app. Cancellation requests terminate the tracked child process and return a structured cancelled status without crashing Electron.

Completed runs appear in a short in-memory history with status, timestamp, exit code, and a small plain-text output preview. The history is not persisted to disk.

## Windows And WSL2 Note

OpenClaw support on Windows may require WSL2 depending on the current OpenClaw version and daemon/channel needs. This app currently runs the native PowerShell/npm path only after confirmation. If install or onboarding fails on native Windows, continue manually in WSL2 if that better matches the OpenClaw requirements.

## Preview Boundary

V1.2 keeps the existing controlled OpenClaw install/onboarding path only under Advanced Tools / Experimental with one-run-at-a-time enforcement, cancellation, lifecycle status, and in-memory run history. FFmpeg, OBS, arbitrary commands, screen recording execution, and other installers remain disabled.
