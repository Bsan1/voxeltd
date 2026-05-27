# Codex Workflow

## V1.2 Manual Setup Posture

Codex remains the default intended implementer concept for this project. The desktop assistant prepares Unity projects for Codex-first work, while OpenClaw Unity Plugin provides the required Unity inspection/control layer.

Claude reviewer/debugger/fallback, Gemini search/visual/concept help, OpenCode, OpenRouter, and local/open-source model roles remain optional future or supplemental ideas. The project does not store their API keys or provide model-role management UI in V1.2.

The app is a Manual Setup Companion: copy commands, run them in a real terminal, return to the app, and verify. Main-flow verification may run read-only checks, but the main setup path does not run installers or onboarding automatically.

## Recommended Path

1. Create GitHub repo.
2. Clone repo locally.
3. Add this bootstrap package.
4. Commit initial files.
5. Open the repo in Codex CLI, Codex App, or Codex Web.
6. Start with `prompts/codex/01-initial-electron-app.md`.

## Codex CLI

From repo root:

```powershell
codex
```

For non-interactive later tasks:

```powershell
codex exec "Summarize this project and run available checks."
```

## Codex App / Worktrees

Use worktrees for larger tasks so each task has an isolated diff.

Suggested pattern:

- One thread for app skeleton.
- One thread for setup-core generators.
- One thread for security model.
- One thread for OpenClaw remote-channel docs.

## GitHub + PR Review

After Codex creates a branch or diff:

1. Review locally.
2. Push branch.
3. Open PR.
4. If Codex code review is enabled, request:

```text
@codex review for security regressions, Electron unsafe IPC, and accidental secret storage.
```

## Done Definition

A Codex task is done only when it reports:

- changed files
- install/run commands
- test/check result
- known risks

## Local Validation

For desktop app changes, run:

```powershell
npm.cmd install
npm.cmd run typecheck
npm.cmd run build
npm.cmd run smoke
npm.cmd audit --audit-level=high
```

Use `npm.cmd run dev` for the manual preview-release walkthrough in `docs/RELEASE_CHECKLIST.md`.

For V1.2 manual setup work, manually confirm:

- the app opens to Setup, not a dashboard-heavy command page
- the flow shows Select Unity Project, Write Setup Files, Codex, OpenClaw CLI, Gateway, patched fork get/update, Gateway-side extension build/copy, Unity Package install, Unity connect, Unity bridge verify, `test.driveCharacter` validation, optional Telegram, and Final Verify
- Codex install is shown as `npm.cmd install -g @openai/codex`
- OpenClaw install is shown as `npm.cmd install -g openclaw@latest`
- OpenClaw verify is shown as `openclaw.cmd --version`
- onboarding is shown as `openclaw.cmd onboard`, with daemon onboarding optional
- Gateway verification is `openclaw.cmd gateway status`; OpenClaw Control opens visible Gateway/TUI/log terminals and does not use hidden background Gateway startup
- the patched fork step presents the shareable fork and UPM Git URLs first; any local path is developer fallback only
- the required Unity Gateway Extension step shows `npm.cmd install`, `npm.cmd run build`, the `OpenClawPlugin~` copy step into `%USERPROFILE%\.openclaw\extensions\unity\` (including `dist/index.js`), `openclaw.cmd plugins registry --refresh`, `openclaw.cmd plugins doctor`, and `http://127.0.0.1:18789/unity/status`
- the Unity Package step warns that `Packages/com.tomleelive.openclaw-unity-plugin/` embedded packages override manifest file/Git URL sources
- the bridge validation step uses `tools.list`, `app.getState`, `app.setPlayMode`, and `test.driveCharacter`; movement success requires non-zero transform or rotation delta
- the verified working configuration callout references OpenClaw 2026.5.7 and the optional `openai-codex/gpt-5.5` model smoke test
- verify buttons run checks only and do not run installers
- Advanced Tools starts collapsed
- no token inputs or freeform command input exist
- Final Verify / Bring System Online is verify-only

For the V1.0.2 guided-flow UI, manually confirm that setup checks stay visible while scrolling, repeated checks re-focus the same unresolved required issue, the `Go to first issue` fallback works, resized windows stay readable, and the Prompt Library keeps long examples out of the primary install path.

For V1.0.3 cleanup work, manually confirm that App Downloads / Cache cleanup shows the exact app-managed folder, requires confirmation, and never targets Unity project files or release artifacts. Telegram setup guidance must not ask for or store tokens.

For V1.0.4 targeting work, manually confirm that Advanced Maintenance is visible near the bottom of Setup Wizard but not numbered, and that Run Setup Checks targets the first required warning or fail in setup flow order.

For V1.0.6 OpenClaw setup work, manually confirm that CLI detection, basic onboarding, and optional daemon onboarding are separate. `openclaw.cmd --version` must not be described as full OpenClaw setup completion, and onboarding guidance must not ask the user to paste secrets into this app.

For V1.0.7 OpenClaw setup work, manually confirm that Setup Wizard actions open the existing Controlled Command Runner confirmation, that the decision guide recommends `openclaw.cmd onboard` by default, and that daemon onboarding is recommended only for advanced always-on/background behavior.

For V1.1 packaging work, run `npm.cmd run package:win` after validation and keep generated `release/` artifacts out of Git.

## OpenClaw Unity Loop Setup Guide

The desktop app surfaces this workflow in the separate **OpenClaw Loop Guide** tab, not inside Setup. The guide explains the intended runtime loop: start Gateway/TUI from OpenClaw Control or the guide's controlled **Start Loop** action, connect Unity to `http://localhost:18789`, write the Unity feature/mechanic task inside OpenClaw TUI, let the configured OpenClaw agent/model implement, validate through the Unity bridge, fix failures, and report evidence. When a valid Unity project is selected, the app launches the visible OpenClaw TUI terminal with that project as the PowerShell working directory so the agent does not default to `.openclaw\workspace`.

The app remains a setup guide. It does not directly control Claude or Codex, does not execute hidden model/API calls, and does not send feature tasks. It provides setup context, generated workflow files, a short starter message for OpenClaw TUI, a controlled starter that copies that message for manual paste, and collapsed reference material.

Loop readiness should confirm the selected Unity project, `ProjectSettings/ProjectSettings.asset`, generated workflow files, Gateway status, and a connected Unity bridge session before the user sends the implementation task. Generated Unity projects get `AGENTS.md`, `docs/CODEX_WORKFLOW.md`, and the `Docs/...` setup files so the OpenClaw TUI can read the loop instructions from the selected Unity workspace.

The older Claude/Codex prompt-passing material below is retained as optional planning/review reference. The normal Unity implementation loop starts in OpenClaw TUI.

Roles:

- OpenClaw TUI is the main control point for Unity feature tasks.
- The configured OpenClaw agent/model, such as Codex/GPT-5.5, implements inside the OpenClaw session.
- OpenClaw Unity bridge is the runtime validator for scene/component/Play Mode evidence.
- Claude is optional for planning or review when helpful.
- This app is the setup companion and loop guide.

Optional planning/review reference has four manual stages.

### Stage A - Plan with Claude

- Summarize the current repo + Unity state in plain text (use `git status`, `git log -1 --oneline`, generated files list).
- Produce an implementation plan with file scope, edits, and risks.
- Define test criteria: what observable state proves success vs. just compile-success.
- Prepare a tight Codex prompt that lists the files to edit, the contract, the validation commands, and what to report back.

### Stage B - Implement with Codex

- Copy the Codex prompt produced in Stage A into Codex CLI/App/Web.
- Keep scope tight: only the files in the prompt.
- Run `npm.cmd run typecheck`, `npm.cmd run build`, `npm.cmd run smoke` if app code changed.
- If quota allows, run OpenClaw validation as well (Stage C).
- Codex reports back: files changed, commands run, test results, anything it could not do.

### Stage C - Validate with OpenClaw

- Verify Unity runtime behavior through OpenClaw. Do not rely on compile-success alone.
- Use deterministic bridge validation where possible (`test.driveCharacter`).
- Required pre-checks before runtime validation:

```powershell
openclaw.cmd gateway status
openclaw.cmd unity status
```

```powershell
$body = @{ tool = 'tools.list'; parameters = @{} } | ConvertTo-Json -Depth 5
Invoke-RestMethod http://127.0.0.1:18789/unity/execute -Method Post -Body $body -ContentType 'application/json' | ConvertTo-Json -Depth 20
```

- `tools.list` should report `source: "unity-bridge"`. If it does not, the bridge is not loaded by the running Gateway and validation results cannot be trusted.
- Use `app.getState` and `app.setPlayMode` to control Play Mode through OpenClaw.
- Use `test.driveCharacter` as the deterministic setup validation.
- OpenClaw command success is not feature success. Feature success requires observable transform, rotation, state, component, or log evidence.
- End every runtime validation with Play Mode stopped and no scene save (unless explicitly approved).

### Stage D - Review / Handoff

- Claude (or the human) reviews Codex output and OpenClaw evidence.
- Classify failures using the table below.
- Prepare the next prompt (back to Stage A or B as needed).
- Update handoff docs only when something changes that future agents must know.

## Failure Classification Table

| Category | Meaning | Next step |
|----------|---------|-----------|
| Compile fail | Code does not compile. | Codex fixes compiler errors first. |
| Unity Test Runner fail | Feature logic or test expectation issue. | Inspect test logs; fix code or test. |
| OpenClaw unavailable | Runtime validation blocked by Gateway/session/setup issue. | Troubleshoot gateway/session. Do not blame the feature yet. |
| Bridge tool missing/failing | OpenClaw connected but required tool missing. | Fix bridge/tool or use supported validation route. |
| Feature behavior fail | OpenClaw works but feature did not behave correctly. | Codex fixes gameplay logic. |
| Test hook missing | Feature is not observable/testable. | Add debug/test hook or observable state. |
| Manual action required | Automation cannot proceed safely. | Ask user exact action; avoid vague requests. |

## No-Image-Debugging Rule

- Do not rely on screenshots or image reading for debugging yet.
- Use text/log/structured data first:
  - Unity Console text
  - OpenClaw JSON results from `/unity/execute`
  - `git diff`
  - Unity logs
  - Test Runner XML
  - Component/state/transform inspection through the bridge
- Screenshot/visual capture can be added later as experimental.
- For now, if visual confirmation is needed, ask the user to describe what they see in text, or use structured OpenClaw scene data instead.

## Manual Interaction Policy

- Agents should not assume the user will manually click Unity, focus Game View, reconnect panels, or stop Play Mode.
- Try Unity/OpenClaw automation first.
- If manual action is truly required, the agent must ask before continuing.
- The request must be exact:
  - what the user should do
  - why automation cannot do it
  - when to do it
  - what will be verified after

Good example: "Please click once inside the Unity Game/Play view, then say `clicked`. I will rerun `input.keyPress` and compare the transform delta."

Bad example: "Make sure Unity is connected."

Ask before any of the following:

- saving a scene
- closing Unity
- changing Project Settings
- deleting folders
- killing unknown processes
- GitHub push or force push
- OpenClaw config reset

Safe without asking:

- `git status`, `git diff`, read files
- `npm.cmd run typecheck`, `npm.cmd run build`, `npm.cmd run smoke`
- `openclaw.cmd gateway status`, `openclaw.cmd unity status`
- read-only `/unity/status` and inspection-only `/unity/execute` calls

## Feature Test Contract

For every new Unity feature, the agent must define a small contract before implementing:

- What object/component should exist?
- What observable state changes?
- What method or event triggers it?
- What is pass/fail (exact threshold)?
- Can it be tested in EditMode?
- Can it be tested in PlayMode?
- Can OpenClaw validate it?
- What cleanup is required?
- Does it require a deterministic test hook?

Example - Feature: Door opens

Observable:

- Door rotation changes by > 80 degrees
- `isOpen` state becomes `true`
- Collider state expected

OpenClaw validation:

1. Find the `Door` object.
2. Record rotation.
3. Trigger interaction or call test hook.
4. Wait.
5. Record rotation.
6. Delta > 80 degrees = pass.
7. Stop Play Mode.
8. No scene save.

## First Feature Automation Smoke Test - OpenClawValidationKeyMover

Goal: prove the full loop end-to-end:

Codex adds a small Unity behavior &rarr; Unity compiles &rarr; OpenClaw validates runtime behavior &rarr; cleanup happens.

Canonical script path (project-relative, primary): `Assets/OpenClawValidation/OpenClawValidationKeyMover.cs`.

Example local tested path (do not hardcode in prompts or docs): `C:\Users\sanba\OneDrive\Documents\Unity\TestNetwork\Assets\OpenClawValidation\OpenClawValidationKeyMover.cs`. If the selected Unity project changes, resolve the script under that project's `Assets/OpenClawValidation/` folder instead.

The Unity AI Setup Assistant now ships a reusable `OpenClawValidationKeyMover.cs` template through `packages/unity-templates`. The "Write Setup Files" step writes it under `Assets/OpenClawValidation/` in the selected Unity project. It is an optional New Input System validation helper, not production gameplay code, and is safe to delete after smoke testing. The script reads `UnityEngine.InputSystem.Keyboard.current` directly, exposes `moveSpeed` (2), `rotateSpeed` (90), and `enableExtraKeys` (true), and is guarded by `#if ENABLE_INPUT_SYSTEM` so it compiles even before the New Input System package is enabled. It is independent of `FPSController` and any legacy input paths. The OpenClaw Loop Guide renders an 11-step OpenClaw validation checklist (gateway/unity status, tools.list source, temp `OpenClawValidation_NewInputMover_Test` object, `app.setPlayMode`, `input.newInputKeyPress` sequence over W/D/I/L/Q/O, delta verification, Play Mode stop, cleanup) plus the `app.getState` vs `/unity/status` lag note.

Test idea:

- Add a tiny `OpenClawValidationKeyMover` script under `Assets/OpenClawValidation/` that reads `UnityEngine.InputSystem.Keyboard.current` directly.
- Map W/A/S/D for normal movement and I/J/K/L as alternate movement keys; Q/E and U/O for optional rotate.
- Create a temporary `OpenClawValidation_NewInputMover_Test` GameObject (or attach to an inactive test object) at Play Mode start.
- Use `input.newInputKeyPress` to drive each key for ~1 second.
- Record position/rotation before and after and require non-zero delta.
- Stop Play Mode and remove the temporary object.
- Do not save the scene.

This is separate from `test.driveCharacter`. `test.driveCharacter` is a deterministic setup validation against the existing legacy controller. `OpenClawValidationKeyMover` is the project-side feature automation smoke test that proves the New Input System injection path actually drives a real Unity component.

Compared paths:

1. Existing `CharacterPlayer` / `FPSController` - legacy `Input.GetAxisRaw`; New Input injection does not move it. Expected.
2. New `OpenClawValidationKeyMover` - uses New Input System; should move from `input.newInputKeyPress`. Proves the future-controller path.
3. `test.driveCharacter` - deterministic setup validation; already passes no-manual-click.
4. OS SendInput / `input.keyPress` / `input.mouseMove` - experimental, foreground-dependent, not the primary validation path.

## Codex-only Loop (Codex + OpenClaw)

The desktop app also surfaces a **Codex-only Loop (Codex + OpenClaw)** reference inside the OpenClaw Loop Guide for situations where Claude quota is unavailable or one OpenClaw agent/model should own the loop end-to-end. The normal path still starts in OpenClaw TUI and validates through the Unity bridge. Claude remains optional for planning and review. Both variants still require OpenClaw runtime validation for any Unity behavior change; compile success is not behavior success.

Codex-only stage flow:

1. Context Intake - inspect `git status`, recent commit, relevant docs, latest handoff, current setup state, and identify scope without broad rewrites.
2. Plan + Safety - write a short plan, define pass/fail and observable Unity state, identify required OpenClaw validation, define cleanup, and respect the hard rules (no scene save, no OpenClaw config reset, no push without approval).
3. Implement - make scoped changes only, keep tests observable, add deterministic test hooks when needed, never debug from screenshots, and never add token inputs or freeform command boxes.
4. Validate - run `npm.cmd run typecheck`, `npm.cmd run build`, `npm.cmd run smoke`, and `npm.cmd audit --audit-level=high`. For Unity tasks, also run `openclaw.cmd gateway status`, `openclaw.cmd unity status` (prefer `http://127.0.0.1:18789/unity/status` for live truth), `tools.list` with `source: "unity-bridge"`, `app.getState`, `app.setPlayMode`, `test.driveCharacter`, and `input.newInputKeyPress` against `Assets/OpenClawValidation/OpenClawValidationKeyMover.cs` when New Input System feature validation applies.
5. Failure Classification - classify against the same table (compile fail, Unity Test Runner fail, OpenClaw unavailable, bridge tool missing/failing, feature behavior fail, test hook missing, manual action required, blocked hard-stop action).
6. Report / Next Prompt - report files changed, commands run, validation results, Unity/OpenClaw final state, Play Mode final state (must be stopped), cleanup result, remaining risks, and the exact next prompt if another pass is needed.

The copyable Codex-only prompt template lives in the OpenClaw Loop Guide optional reference; use it instead of repeating the rules in chat. For unattended runs, the template explicitly allows Codex to skip waiting for manual confirmation, mark blocked steps, and proceed straight to cleanup/report.
