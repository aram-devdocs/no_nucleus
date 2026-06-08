# Documentation

Docs describe the **current, technical, game-applied** system. They are reference, not a project journal.

## Rules
- No PM artifacts in the repo: no status/build-status pages, dev logs, plan/ledger/backlog/decision files,
  per-phase progress, or review write-ups. Working state for an AI loop lives in the agent's own prompt, not in
  committed files.
- Keep only: this `AGENTS.md` tree, `.agents/rules/`, lean READMEs (root + per-app/lib where they add value),
  and current technical references (e.g. testing, deployment).
- Comments are self-documenting code: explain the non-obvious *why*, not narration, history, or restated code.
  Strip comment bloat.
- Human-readable prose. Avoid AI-tell filler ("comprehensive", "leverage", "seamless", "dive in").
- XML `<summary>` on the public package API; skip trivial/internal members.
