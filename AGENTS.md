# ScreenCanvas agent instructions

1. Before any task, read in order: this file, `AI/PROGRESS.md`, `AI/CONTEXT.md`, `AI/DECISIONS.md`. Treat `AI/PROGRESS.md` (its newest checkpoint) as the current source of truth. If a file is missing, create it from verified project information only and record that in `AI/PROGRESS.md`.
2. Before changes, report current status, completed work, active work, recommended next actions, last model, last tool/agent, last update time, project state, and pending actions.
3. Use this deployment sequence: Local Development → Development Environment → Validation/Testing → deploy the same validated package to Production.
4. For Azure work, first run `az account show` and follow the Azure guidance in `AI/CONTEXT.md`. Do not assume access limitations.
5. Do not create Azure resources, databases, storage accounts, environments, or services unless explicitly required. Never store secrets, credentials, connection strings, API keys, or passwords in plaintext.
6. Preserve existing product behavior unless the task explicitly requests functional changes. Do not overwrite unrelated user changes.
7. Record progress continuously, not only at the end. After every meaningful completed or blocked operation (feature, fix, build, test, validation, blocker, failure that changes the plan), append to the Checkpoints section of `AI/PROGRESS.md`: `[YYYY-MM-DD HH:mm IST] Task: <task>. Result: <result>. Files: <important files>. Validation: <check>. Next: <next action>.` Keep each entry at most 750 characters. Do not log low-value actions (reading files, searches).
8. Keep `AI/CONTEXT.md` limited to durable implementation knowledge. Keep `AI/DECISIONS.md` limited to significant decisions and rationale.
9. Before ending a working session, append one handover checkpoint (at most 750 characters): `[YYYY-MM-DD HH:mm IST] HANDOVER: <state>. Completed: <work>. Validation: <results>. Pending/Blockers: <items>. Next: <exact next action>.`

