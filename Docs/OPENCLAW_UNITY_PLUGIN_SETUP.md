# OpenClaw Unity Plugin Setup

Codex remains the default implementer. OpenClaw Unity Plugin is the required Unity inspection/control layer used around the Codex-first manual setup workflow.

Important: the Unity package alone does not make OpenClaw CLI/Gateway integration work. The Gateway-side Unity extension from `OpenClawPlugin~` must be installed first.

## Verified Working Configuration

- OpenClaw CLI/Gateway version `2026.5.7` on Windows.
- Patched compatibility fork: `https://github.com/Bsan1/openclaw-unity-pluginTest` branch `fix/openclaw-latest-compat`.
- Gateway-side extension installed at `%USERPROFILE%\.openclaw\extensions\unity\` with compiled runtime `dist/index.js`.
- Optional coding model: `openai-codex/gpt-5.5` (model failures are independent of the Unity bridge).

The upstream plugin README lists OpenClaw `2026.2.3+` as the minimum requirement, and the patched branch removes the removed `registerHttpHandler` API, adds session retirement on Editor/Play transitions, restores the stale-session timeout to 120000ms, and tolerates Infinity/NaN as null.

## Get / Update Patched OpenClaw Unity Fork

Shareable Unity Package Manager Git URL:

```text
https://github.com/Bsan1/openclaw-unity-pluginTest.git#fix/openclaw-latest-compat
```

If you maintain a local developer checkout, use your own path in place of `<LOCAL_OPENCLAW_UNITY_PLUGIN_FORK_PATH>`. Do not commit or share machine-specific paths.

If the repo does not exist locally and you need a local checkout for Gateway extension build work:

```powershell
git clone https://github.com/Bsan1/openclaw-unity-pluginTest.git <LOCAL_OPENCLAW_UNITY_PLUGIN_FORK_PATH>
cd <LOCAL_OPENCLAW_UNITY_PLUGIN_FORK_PATH>
git checkout fix/openclaw-latest-compat
```

If the repo already exists locally:

```powershell
cd <LOCAL_OPENCLAW_UNITY_PLUGIN_FORK_PATH>
git fetch origin
git checkout fix/openclaw-latest-compat
git pull origin fix/openclaw-latest-compat
```

Check patched fork status:

```powershell
cd <LOCAL_OPENCLAW_UNITY_PLUGIN_FORK_PATH>
git status
git branch --show-current
git log -1 --oneline
```

## Default Gateway URL

```text
http://localhost:18789
```

## Required OpenClaw Gateway Extension Bridge

```powershell
cd <LOCAL_OPENCLAW_UNITY_PLUGIN_FORK_PATH>\OpenClawPlugin~
npm.cmd install
npm.cmd run build
New-Item -ItemType Directory -Force "$env:USERPROFILE\.openclaw\extensions\unity" | Out-Null
Copy-Item -Recurse -Force "<LOCAL_OPENCLAW_UNITY_PLUGIN_FORK_PATH>\OpenClawPlugin~\*" "$env:USERPROFILE\.openclaw\extensions\unity\"
openclaw.cmd plugins registry --refresh
openclaw.cmd plugins doctor
openclaw.cmd gateway status
openclaw.cmd unity status
Invoke-RestMethod http://127.0.0.1:18789/unity/status | ConvertTo-Json -Depth 5
```

- Copying `OpenClawPlugin~` installs the Gateway-side bridge files, including the compiled `dist/index.js` runtime.
- `openclaw.cmd plugins registry --refresh` makes the Gateway aware of the new extension.
- `openclaw.cmd gateway --force` starts the Gateway in the foreground; `Start-Process -FilePath "openclaw.cmd" -ArgumentList "gateway" -WindowStyle Hidden` is the background alternative.
- `http://127.0.0.1:18789/unity/status` is the real Gateway session verification and must show the endpoint loaded before expecting CLI/Gateway-to-Unity communication.
- If `openclaw.cmd unity status` says no sessions but the HTTP endpoint lists sessions, trust the HTTP endpoint because sessions live in the running Gateway process.
- If the OpenClaw TUI says it cannot access Unity while `/unity/status` lists a session, update the Gateway extension from `OpenClawPlugin~`, restart Gateway, and verify `http://127.0.0.1:18789/unity/execute` with `app.getState`.

## OpenClaw Onboarding Result

If `openclaw.cmd onboard` ends with `Onboarding complete`, basic OpenClaw onboarding finished. Continue with `openclaw.cmd gateway status`. If Gateway is not running, start it manually with `openclaw.cmd gateway --force`. If the output says `Use the dashboard link above`, use the dashboard/control UI only if needed, or try `openclaw.cmd dashboard`.

- `Workspace backup` is informational and is not an error.
- `Security` is important. Keep OpenClaw personal/local by default and avoid exposing it publicly.
- `Shell completion failed` is not fatal and should not block setup; run `openclaw.cmd completion --install` later if desired.
- `Web search enabled` means OpenClaw can use the configured search provider.
- Provider: Gemini / Google Search means web lookup is likely configured, not that Gemini is the main implementer.
- API keys belong in OpenClaw config or official provider config. Do not paste them into this app.

## Unity-Side Setup

1. Open the Unity project.
2. Open `Window > Package Manager`.
3. Select `+ > Add package from git URL`.
4. Prefer the shareable Git URL `https://github.com/Bsan1/openclaw-unity-pluginTest.git#fix/openclaw-latest-compat`.
5. Open `Window > OpenClaw Plugin`.
6. Gateway URL: `http://localhost:18789`.
7. API Token: leave empty unless OpenClaw config explicitly requires it.
8. Add `OpenClawBridge` to the active scene if Unity asks for it.
9. Click `Connect` / `Force Reconnect`.
10. Enter Play Mode only when explicitly testing Play Mode behavior.
11. Run `openclaw.cmd unity status` (and prefer `http://127.0.0.1:18789/unity/status` for ground truth).

## Embedded Package Warning

Unity may load an embedded package from `Packages/com.tomleelive.openclaw-unity-plugin/`. This overrides manifest `file:D:/...` references and Git URL package references. Check `Packages/packages-lock.json` before assuming the patched fork is loaded. If source is `embedded`, intentionally sync patched Runtime files into the embedded package, or later canonicalize by removing the embedded folder only after explicit approval.

## Primary Automated Unity Validation

Use `test.driveCharacter` through OpenClaw Gateway `/unity/execute`. It starts or uses Play Mode, attaches a temporary runtime driver to `CharacterPlayer`, verifies real transform/rotation delta, removes the driver, stops Play Mode, and does not save the scene.

Movement success requires non-zero transform or rotation delta. Command success alone is not movement success.

Verified no-manual-click values: `moveZ=1 duration=1 speed=2` produced Delta z about `+2.0035`; `moveX=1 duration=1 speed=2` produced Delta x about `+2.0009`; `rotateY=45` produced Delta yaw about `+45.07` degrees; backend was `CharacterController`; driver attached/removed; no scene save; no manual Unity click required.

New Input System injection (`input.newInputKeyPress`, `input.newInputMouseMove`) is a future-controller path. Current `FPSController` uses legacy `Input.GetAxisRaw` / `Input.GetKey`, so injection can succeed without moving it. OS SendInput / `input.keyPress` / `input.mouseMove` is experimental and foreground-dependent.

## Known Limitations

- A Gateway service warning about embedded `OPENCLAW_GATEWAY_TOKEN` may remain; this app must not try to fix or reset it automatically.
- Console log query may say `logger not initialized` until the Unity Editor reloads the patched package and `captureConsoleLogs` is enabled.
- Screenshot / visual capture may not be supported in the current bridge.
- OS SendInput / `input.keyPress` / `input.mouseMove` is experimental and foreground-dependent.
- `input.keyPress` success alone is not movement success.
- Inactive objects may be invisible to `GameObject.Find`-based tools.
- Editor open/close should not be tested on unsaved user projects.
- A model/provider failure (for example missing or wrong model) does not mean the Unity bridge failed; treat the two independently.

## Inspection-First Policy

- Start in inspection-first mode.
- Keep Codex as the default implementer.
- Use the plugin for Unity inspection and tightly controlled editor interaction only.

## Allowed Safe Tasks

- Inspect active scene hierarchy.
- Read Console logs and errors.
- Capture screenshots when useful.
- Inspect prefab, component, and scene data before proposing changes.

## Blocked Unless Explicitly Approved

- Scene or prefab modification.
- Saving production scenes after plugin operations.
- Arbitrary code execution.
- Package installation or broader project mutation.

Permanent scene or prefab changes should preferably be reproduced through Unity Editor automation scripts.

## OpenClaw Inspection Prompt

```text
Use the Unity plugin in inspection-first mode.
Do not modify or save the scene.
Check active scene hierarchy, Console errors, relevant prefab/component data, and screenshots if useful.
Compare the result against AGENTS.md and Docs/UNITY_VALIDATION_CHECKLIST.md.
Report only concrete issues, missing references, risky setup, or visual/test evidence.
```
