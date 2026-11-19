# Chat history

Append-only record of every exchange with an AI agent on this project, written by the owner's claude-chat-history hooks. This project opts in through [.claude/chat-history.json](../../.claude/chat-history.json) (issue #361).

## Local only

Week files (`YYYY-Www.md`), `archive/`, the `.pending/` folder and the `.lock` file are git-ignored and must never be committed: prompts are kept word for word and can contain secrets. This README is the only tracked file in this folder. The hooks refuse to write anything that git does not ignore. New clones do not include past logs.

## Layout

| Path | Purpose |
| --- | --- |
| `YYYY-Www.md` | ISO-week logs (Monday to Sunday). Entries go only to the current week. Local only. |
| `archive/vscode-era.md` | Frozen VS Code era log (entries [1] to [136]). Never appended to. Local only, when present. |

## How entries are written

- **Session start:** the hook gives the agent the summary instructions and the latest entries, newest first, within 8,000 characters. Open older week files or the archive only when a task needs that history.
- **Prompt:** the hook saves the prompt word for word, with its time and the session id, as a turn record in `.pending/<session id>/`.
- **End of turn:** before ending its turn, the agent writes `.pending/<session id>.summary.json`. The hook validates it, appends the numbered entry to the current week file and deletes the pending files. If the summary is missing or invalid, the hook sends the agent back once, then writes the entry marked "summary missing".
- Numbering is global across week files and the archive. Entries [1] to [485] were written by hand, in the older format below; the hooks continue from the highest local number.

## Summary file

A JSON object with five keys:

```json
{
  "title": "One line",
  "from": "owner",
  "assistant": "What the agent answered and did",
  "filesChanged": "Files changed, or none",
  "decisions": ["Decisions or context for future agents"]
}
```

`from` is `owner` when the owner typed the prompt, or `automated (<what kind>)` for a task notification, a scheduled task or a message from another session.

## Entry format

````text
## [N] YYYY-MM-DD HH:MM — <title>
**From:** owner | automated (<what kind>)
**Session:** <Claude Code session id>
**User:**
```text
<prompt, word for word>
```
**Assistant:** <summary of the answer and actions taken>
**Files changed:** <list or "none">
**Decisions / context for future agents:**
- <decision>
````

Entries written by hand before the opt-in use the older format: `**User:**`, `**Assistant:**`, `**Files changed:**` and `**Decisions / context for future agents:**` on one line each, with no `**From:**` or `**Session:**` line.

## Without the hooks

An agent in a tool that does not run Claude Code hooks (for example Cursor) appends its entries by hand, in the format above, to the current week file, continuing the numbering. It writes `**Session:** none (written by hand)`.

## Rules

- Append only: entries are never rewritten or deleted, except by a move the owner orders.
- A session logs into the project it was opened in. Work on another project belongs in a session opened in that project, so its turns land in that project's log.
