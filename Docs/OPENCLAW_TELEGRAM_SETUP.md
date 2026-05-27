# OpenClaw Telegram Setup

## Purpose

Telegram is an optional remote control surface for OpenClaw. OpenClaw can route Telegram messages into inspection-first workflows that use the OpenClaw Unity Plugin for Unity scene, Console, screenshot, and status checks.

In V1.2, this remains optional setup/documentation guidance only. The assistant does not implement a real Telegram connection, does not validate Telegram credentials, does not upload files, and does not store bot tokens or channel secrets. Telegram not configured never blocks required setup.

## Intended Architecture

```text
Telegram message
-> OpenClaw
-> OpenClaw Unity Plugin
-> Unity scene/log/screenshot inspection
-> response back to Telegram
```

## OpenClaw Install Commands

```powershell
npm.cmd install -g openclaw@latest
openclaw.cmd --version
openclaw.cmd onboard
openclaw.cmd onboard --install-daemon
```

Windows users may need WSL2 depending on the current OpenClaw version and daemon/channel needs. The desktop assistant recommends running these commands manually in a real terminal, then returning to the app to verify. Advanced Tools may still contain experimental confirmation-gated runner actions; they are not the primary setup path.

`openclaw.cmd --version` only confirms that the OpenClaw CLI is detected. It does not prove onboarding is complete. Basic setup uses `openclaw.cmd onboard`. `openclaw.cmd onboard --install-daemon` is optional and advanced for background/remote service behavior.

`openclaw.cmd onboard` can require terminal-style interaction. It may ask about providers, API keys, Gateway, channels, Telegram, and daemon/background mode. If onboarding asks for provider or channel secrets, enter them only into the actual OpenClaw process/tool, not into this assistant.

Practical choices: use normal/manual mode for a first local test if available, use the default local Gateway (`http://localhost:18789`) if offered, choose Telegram only if remote control is needed now, and choose OpenAI/OpenAI-compatible if unsure about the current project default. OpenRouter may be useful later if one API/router should manage multiple models, but this generated setup does not add provider profile UI or API key storage.

Use `openclaw.cmd onboard` for local setup, first-time Unity inspection prep, and Telegram testing while actively working. Use `openclaw.cmd onboard --install-daemon` only for advanced always-on/background behavior; it may configure a background service and is not required for basic Unity setup.

After onboarding, verify Gateway manually with `openclaw.cmd gateway status`. If Gateway is configured but not running, start it in the foreground with `openclaw.cmd gateway --force`, or hidden in the background with `Start-Process -FilePath "openclaw.cmd" -ArgumentList "gateway" -WindowStyle Hidden`.

## Optional Model Smoke Test

```powershell
openclaw.cmd agent --agent main --message "Reply with model-ok only." --timeout 120
```

OpenClaw 2026.5.7 has been verified with `openai-codex/gpt-5.5` configured. A model failure (for example unknown model) is independent of the Unity bridge; run `openclaw.cmd configure` or `openclaw.cmd models list` to pick a working model.

## Reading OpenClaw Onboarding Final Output

If `openclaw.cmd onboard` ends with `Onboarding complete`, basic OpenClaw onboarding finished. Continue with `openclaw.cmd gateway status`. If Gateway is not running, start it manually with `openclaw.cmd gateway --force`. If the output says `Use the dashboard link above`, the dashboard/control UI is available if needed; you can also try `openclaw.cmd dashboard`.

- `Workspace backup` is informational and is not an error.
- `Security` is important. Keep OpenClaw personal/local by default and avoid public exposure.
- `Shell completion failed` is non-blocking. Continue setup normally and later run `openclaw.cmd completion --install` if desired.
- `Web search enabled` means OpenClaw can use the configured search provider.
- Provider: Gemini / Google Search means web lookup is likely configured. It does not mean Gemini is the main implementer.
- API keys should stay inside OpenClaw official config/provider config. Do not paste them into this app.

## Telegram Setup Checklist

- Create a Telegram bot with BotFather.
- Use `/newbot` in BotFather.
- Do not paste the bot token into this app.
- Put the token only in OpenClaw official config, onboarding, or environment setup.
- If Telegram does not appear in OpenClaw onboarding, skip channel setup and configure Telegram later.
- Do not send secrets through Telegram commands.

## Important Boundaries

- Do not ask for the Telegram bot token in this app.
- Do not store Telegram tokens in generated project files.
- Do not implement a real Telegram connection in this project yet.
- Do not validate Telegram credentials in this app.

## No Secret Storage In Repo Files

Do not commit Telegram tokens, API keys, or OpenClaw private config. Use temporary session variables for short tests, then prefer user-specific environment variables, an OS keychain, or OpenClaw secure config.

## Safe Telegram / OpenClaw Commands

- inspect Unity scene
- get Console errors
- take screenshot
- list selected scene objects
- run setup checks
- summarize generated files
- report validation status
- check OpenClaw Unity Plugin status

## Dangerous Commands Requiring Explicit Approval

- save scene
- modify scene
- delete files
- install packages
- run arbitrary code
- execute shell commands
- push to git
- send API keys or secrets
- change ProjectSettings
- record screen/video
- upload files

## Prompt Examples

### Prompt A: Safe Telegram Unity inspection

```text
Use the Unity plugin in inspection-first mode.
Do not modify or save the scene.
Check active scene hierarchy, Console errors, relevant prefab/component data, and one screenshot if available.
Report only concrete issues, missing references, risky setup, or visual/test evidence.
```

### Prompt B: Telegram status command style

```text
Inspect the Unity project status.
Do not modify files or save the scene.
Return Console errors, active scene name, OpenClaw Unity Plugin status, generated-file status, and one screenshot if available.
```

### Prompt C: Approval-required operation

```text
Before making any scene, file, package, shell-command, screen recording, or upload change, ask for explicit approval and summarize the exact action.
```

## Relation To OpenClaw Unity Plugin

Telegram is only the message transport. OpenClaw Unity Plugin remains the Unity inspection/control layer, and the default policy stays inspection-first.
