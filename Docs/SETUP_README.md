# Unity AI Setup README

Generated V1.2 manual setup notes for Codex-first Unity workflows.

Primary setup is copy-paste and verify:

1. Select the Unity project.
2. Write generated setup files.
3. Install and verify Codex: `npm.cmd install -g @openai/codex`, then `codex --version`.
4. Install and verify OpenClaw CLI: `npm.cmd install -g openclaw@latest`, then `openclaw.cmd --version` (verified working version: 2026.5.7).
5. Configure Gateway: run `openclaw.cmd onboard` if needed, then `openclaw.cmd gateway status` (start it with `openclaw.cmd gateway --force` if needed).
6. Get/update the patched fork from `https://github.com/Bsan1/openclaw-unity-pluginTest`; branch should be `fix/openclaw-latest-compat`.
7. Build and install the Gateway-side extension: run `npm.cmd install`, `npm.cmd run build`, copy `OpenClawPlugin~` into `%USERPROFILE%\.openclaw\extensions\unity\` (must include `dist/index.js`), then run `openclaw.cmd plugins registry --refresh`.
8. Install OpenClaw Unity Plugin in Unity from `https://github.com/Bsan1/openclaw-unity-pluginTest.git#fix/openclaw-latest-compat`.
9. Check `Packages/packages-lock.json`; an embedded package at `Packages/com.tomleelive.openclaw-unity-plugin/` overrides manifest/Git sources.
10. In Unity, open `Window > OpenClaw Plugin`, paste the Gateway URL `http://localhost:18789`, leave API Token empty unless OpenClaw config explicitly requires it, add `OpenClawBridge` to the scene if asked, then Connect / Force Reconnect.
11. Verify Unity bridge with `tools.list`, `app.getState`, and `app.setPlayMode` through `http://127.0.0.1:18789/unity/execute`.
12. Run primary automated validation with `test.driveCharacter`. Movement success requires non-zero transform/rotation delta; verified no-manual-click result was Delta z about `+2.0035`, Delta x about `+2.0009`, and Delta yaw about `+45.07` degrees with `CharacterController` backend.
13. Optional model smoke test: `openclaw.cmd agent --agent main --message "Reply with model-ok only." --timeout 120` (OpenClaw 2026.5.7 has been verified with `openai-codex/gpt-5.5`).

OpenClaw install flow:

- `npm.cmd install -g openclaw@latest`
- `openclaw.cmd onboard`
- `openclaw.cmd onboard --install-daemon`

`openclaw.cmd --version` means the CLI is installed/detected; it does not mean onboarding is complete. Treat `openclaw.cmd onboard` as the basic onboarding step. Treat `openclaw.cmd onboard --install-daemon` as optional/advanced for background or remote service behavior.

The desktop assistant main flow does not run these setup commands automatically. Advanced Tools may expose legacy allowlisted runner actions after explicit confirmation, but manual terminal execution is the preferred path. Daemon onboarding may configure a background service. Native Windows setups may need WSL2 depending on the current OpenClaw version and daemon/channel requirements.

OpenClaw onboarding may require interactive terminal input and may ask about providers, API keys, Gateway, channels, Telegram, and daemon/background mode. If the final output says `Onboarding complete`, basic onboarding finished. `Workspace backup` is informational. `Shell completion failed` is not fatal and can be repaired later with `openclaw.cmd completion --install`. The strongest setup verification is `openclaw.cmd gateway status`.

If onboarding says `Web search enabled` and Provider: Gemini / Google Search with an API key stored in config, OpenClaw web lookup is likely configured. That is separate from the main coding model/provider and does not make Gemini the main implementer. Do not paste API keys into this app.

For first-time setup, choose basic/manual/default when available, keep the default localhost Gateway (`http://localhost:18789`) if offered, skip channel setup unless Telegram is being configured now, and choose OpenAI/OpenAI-compatible if unsure. OpenRouter can be used later as one API route for multiple models; this assistant does not configure OpenRouter or store model API keys.

Final Verify / Bring System Online is verify-only in V1.2. Future System Control Center work may check/start/verify OpenClaw Gateway, Telegram channel, OpenClaw Unity Plugin connection, Unity Editor readiness, selected agent profile, optional recorder/evidence tools, and optional model/provider roles. That orchestration is not implemented here.

Do not store API keys, bot tokens, provider secrets, or channel secrets in project files. If onboarding asks for secrets, enter them only into the actual OpenClaw process/tool.

Telegram setup and recording guidance are documentation only in this preview. The assistant does not implement a real Telegram connection or screen recording execution.
