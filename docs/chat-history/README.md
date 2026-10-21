# Chat history

Append-only record of every user/assistant exchange for this project. Standing rules live in [AGENTS.md](../../AGENTS.md); this directory is the chronological audit trail.

## Layout

| Path | Purpose |
| --- | --- |
| `YYYY-Www.md` | Active and past ISO-week logs (Monday–Sunday). Append only to the **current** week. |
| [archive/vscode-era.md](archive/vscode-era.md) | Frozen VS Code–era monolith (entries [1]–[136]). Never append. Historical relative links in that file are excluded from lychee (see `.lychee.toml`); do not rewrite them. |

## Agent reading rules

At the start of a session, read:

1. [AGENTS.md](../../AGENTS.md)
2. [roadmap.md](../roadmap.md) (current milestone / next work)
3. The **current ISO-week** file in this directory (create it if missing)

Do **not** load the full archive by default. Open older week files or the VS Code archive only when you need specific historical context.

## Logging rules

- Append every exchange **before ending the turn**, to the current week’s file.
- Append only; never rewrite or delete past entries.
- Keep **global** entry numbers across files (`[137]`, `[138]`, …). Continuity after the archive: next number is **137**.
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
