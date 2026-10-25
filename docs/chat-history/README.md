# Chat history

Append-only record of every user/assistant exchange for this project. Standing rules live in [AGENTS.md](../../AGENTS.md); this directory is the chronological audit trail on the owner's machine.

## Local only (never commit logs)

Week files (`YYYY-Www.md`) and `archive/` are **gitignored** and must **never** be staged, committed, or included in pull requests. Maintain them locally exactly as before. This README is the only tracked file under `docs/chat-history/`. New clones will not include past logs; create the current ISO-week file when missing and continue the global entry counter from the highest local entry (or start from the archive if present locally).

## Layout

| Path | Purpose |
| --- | --- |
| `YYYY-Www.md` | Active and past ISO-week logs (Monday–Sunday). Append only to the **current** week. Local only. |
| `archive/vscode-era.md` | Frozen VS Code–era monolith (entries [1]–[136]). Never append. Local only when present. |

## Agent reading rules

At the start of a session, read:

1. [AGENTS.md](../../AGENTS.md)
2. [roadmap.md](../roadmap.md) (current milestone / next work)
3. The **current ISO-week** file in this directory when it exists locally (create it if missing)

Do **not** load the full archive by default. Open older week files or the VS Code archive only when you need specific historical context.

## Logging rules

- Append every exchange **before ending the turn**, to the current week’s file.
- Never commit chat-history log files.
- Append only; never rewrite or delete past entries.
- Keep **global** entry numbers across files (`[137]`, `[138]`, …). Continuity after the archive: next number is **137** if starting fresh after the archived range; otherwise continue from the highest existing local entry.
- When the ISO week changes, create a new `YYYY-Www.md` and continue numbering; do not move entries between files.
- Entry format:

```text
## [N] YYYY-MM-DD HH:MM — <short title>
**User:** <verbatim or faithful prompt>
**Assistant:** <summary of answer + actions taken>
**Files changed:** <list or "none">
**Decisions / context for future agents:** <bullets>
```

## Finding the current week

Use the ISO week date of “today” (e.g. 2026-10-21 → `2026-W43.md`). Week files cover Monday–Sunday.
