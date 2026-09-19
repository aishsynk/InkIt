# ScreenCanvas agent instructions

1. Read this file and `AI/PROGRESS.md` before work. Treat `AI/PROGRESS.md` as the current source of truth. Consult `AI/CONTEXT.md` and `AI/DECISIONS.md` for durable context and decisions.
2. Before changes, report current status, completed work, active work, recommended next actions, last model, last tool/agent, last update time, project state, and pending actions.
3. Use this deployment sequence: Local Development → Development Environment → Validation/Testing → deploy the same validated package to Production.
4. For Azure work, first run `az account show` and follow the Azure guidance in `AI/CONTEXT.md`. Do not assume access limitations.
5. Do not create Azure resources, databases, storage accounts, environments, or services unless explicitly required. Never store secrets, credentials, connection strings, API keys, or passwords in plaintext.
6. Preserve existing product behavior unless the task explicitly requests functional changes. Do not overwrite unrelated user changes.
7. After significant work, append a concise entry to `AI/PROGRESS.md` with date/time, model, tools/agents, files modified, completed work, status, blockers, and next actions.
8. Keep `AI/CONTEXT.md` limited to durable implementation knowledge. Keep `AI/DECISIONS.md` limited to significant decisions and rationale.
9. Before ending a working session, append a handover entry to `AI/PROGRESS.md`.

