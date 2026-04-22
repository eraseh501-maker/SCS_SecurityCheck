# AI Agent Guide

This repository uses two knowledge layers:

- `docs/90_全域AI開發守則.md` for cross-project guardrails synchronized from the external `AI-Knowledge` library.
- `docs/10_開發回顧與防錯守則.md` for project-specific lessons learned.

Use the global file first for reusable debugging and delivery rules, then extend it with project-specific constraints from the local lessons file.

## Project Commands

Update this section with the actual commands used by the repository.

## Maintenance Rule

- If a lesson applies across many repositories, update the external `AI-Knowledge` global lessons source (`D:\Vincent\AI-Knowledge\global\90_全域AI開發守則.md`) and then sync this repo.
- If a lesson only applies to this repository, update `docs/10_開發回顧與防錯守則.md`.
- Keep tool-specific instruction files thin and consistent with these two local documents.

## How to Sync Global Rules

```powershell
powershell -ExecutionPolicy Bypass -File scripts\sync-ai-knowledge.ps1
```
