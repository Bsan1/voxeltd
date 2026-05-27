# Agent Rules for OpenClaw Unity Loop

This Unity project uses an OpenClaw-driven implementation loop.

Primary workflow:
1. Claude/planner creates a short implementation plan and validation checklist.
2. Codex/OpenClaw implementer applies scoped Unity code changes.
3. OpenClaw Unity bridge validates the result in Unity.
4. If validation fails, the implementer fixes and retests.
5. If validation passes, report evidence and remaining risks.

Important files:
- AGENTS.md: short rules visible to OpenClaw agents.
- docs/CODEX_WORKFLOW.md: detailed Codex/OpenClaw implementation workflow.
- docs/UNITY_VALIDATION_CHECKLIST.md: Unity runtime validation checklist.
- docs/AI_HANDOFF.md: handoff/final report notes.

Hard rules:
- Do not save Unity scenes unless explicitly allowed.
- Do not push Git.
- Do not reset OpenClaw config.
- Do not delete folders.
- Do not store tokens.
- Stop Play Mode at the end of validation.
- Report changed files, commands run, Unity evidence, pass/fail result, and remaining risks.

For Claude/planner:
Create only the plan, risks, file targets, and validation checklist. Do not implement.

For Codex/implementer:
Read docs/CODEX_WORKFLOW.md when tools are available, implement scoped changes, validate through OpenClaw Unity bridge, and retry fixes when validation fails.

## OpenClaw Unity Loop Rules

- Claude planner creates plan/checklist only; it does not edit files.
- Codex implementer applies scoped changes inside the selected Unity project workspace.
- Unity validator validates through bridge and reports PASS/BLOCKED with evidence.
- Do not save Unity scenes unless explicitly allowed for the current task.
- Do not push Git.
- Do not reset OpenClaw config.
- Stop Play Mode at the end of validation.
- Report files changed, commands run, Unity evidence, pass/fail, and risks.
