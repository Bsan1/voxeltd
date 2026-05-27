# Recording And Screenshot Setup

## Purpose

This workflow keeps screenshots and future video recording setup understandable without executing recording commands from the assistant.

OpenClaw Unity Plugin remains the inspection layer for Unity screenshots, scene status, and Console evidence. Video recording is more sensitive and stays approval-required.

V1.2 is guidance-only for recording. Recording guidance belongs under Advanced Tools. The assistant does not install FFmpeg or OBS, does not start screen recording, and does not send recording output through Telegram.

## Screenshot Workflow

```text
Remote command or local request
-> OpenClaw
-> OpenClaw Unity Plugin
-> Unity screenshot / scene/log inspection
-> response with screenshot or summary
```

Screenshots are safe inspection evidence when they stay focused on Unity review and do not modify or save scenes.

## Safe Screenshot Prompt

```text
Use the Unity plugin in inspection-first mode.
Take one screenshot if available.
Do not modify or save the scene.
Also report Console errors, active scene name, and relevant scene hierarchy issues.
```

## Video Recording Safety Policy

Video recording is approval-required because it may capture:

- private desktop content
- project files
- chat windows
- tokens or secrets
- personal information

Video recording should only start after explicit approval that includes:

- duration
- target window or screen
- whether audio is recorded
- output folder
- whether the file may be sent through Telegram

## FFmpeg Windows Setup

FFmpeg is the initial planned Windows recording backend. The assistant only checks whether `ffmpeg -version` works and shows copyable commands; it does not install or run FFmpeg.

```powershell
winget install --id Gyan.FFmpeg -e
```

```powershell
ffmpeg -y -f gdigrab -framerate 15 -i desktop -t 10 recordings/unity_recording.mp4
```

This example records the full desktop unless later configured for a specific window or region. Close or hide private windows before recording. Do not run this command automatically yet.

## OBS Future Provider

OBS can be used later for higher quality recording. OBS WebSocket may allow controlled remote start/stop in a future task, but this task only documents that direction.

```powershell
winget install --id OBSProject.OBSStudio -e
```

No OBS package or WebSocket client is added here.

## OpenClaw Node Recording Future Note

If OpenClaw node screen recording is available on the user's platform, it may be used later as a provider. Do not assume it works on all systems. The current default plan remains FFmpeg Windows recording.

## Approval-Required Prompt

```text
Before starting any video recording, ask for explicit approval that confirms duration, target window or screen, whether audio is recorded, output folder, and whether the file may be sent through Telegram.
```

## Dangerous Commands Or Actions

- starting a video recording without explicit approval
- capturing the full desktop when a narrower target was expected
- recording audio without approval
- recording screens that may show secrets or personal information
- sending screenshots, generated files, or recordings through Telegram without approval
- running FFmpeg, OBS, or future OpenClaw recording commands automatically

## Remote Channel Relation

Telegram may carry safe screenshot summaries later. Video files, generated project files, and any recording output must not be sent through Telegram unless the approval explicitly covers that transfer.
