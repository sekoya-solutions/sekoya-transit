# Agent Instructions — transport-tycoon-2

Standing rules for **every** AI agent/model/chat working on this project. Read this file and [docs/chat-history.md](docs/chat-history.md) fully at the start of every session.

## 1. Project context
- Transport Tycoon-style game. Inspirations: Transport Tycoon Deluxe (TTD) and Rise of Industry (the first one).
- Engine: **Unity 6.3 LTS**, 2D Universal Render Pipeline (URP), UI Toolkit, pure C# deterministic simulation with commands (see [docs/adr/](docs/adr/README.md); ADRs are the source of truth for technical decisions, read them before coding). Other stack details are decided via ADR with owner approval.
- Product discovery is in progress: the assistant acts as product owner and interviews the owner; answers are summarized in [docs/specs/product-vision.md](docs/specs/product-vision.md), which is the reference for scope (V1/V2/V3+); detailed rules are in [docs/specs/gameplay-systems.md](docs/specs/gameplay-systems.md). Check both before proposing features. All balance values must be data-driven.
- Hard architectural constraints: simulation separated from presentation and deterministic, player actions as commands (future AI opponents in V2, multiplayer in V3+); maps up to 512x512 (sizes S/M/L = 128/256/512); internationalization (i18n) compatible code (English only in V1); robust automated regression tests and continuous integration.
- Owner availability: 20-40 h/week; end of 2026 is an aspiration, not a deadline. Unity version and render pipeline are chosen by the assistant (propose via ADR for approval).
- Targets: Windows PC and WebGL demo.
- Vibe-coded in the sense that the owner steers via chat and AI writes code, but this is a personal project assisted by AI, not a quick delivery.
- Primary purpose: **portfolio** showcasing the owner's skills. Commercial success is secondary.
- Owner: Tech Lead, 15+ years of experience. Very rigorous. Reviews every technical choice and most of the code.

## 2. Quality and working rules
- Apply state-of-the-art coding practices; no quick hacks.
- Documentation must be perfectly ordered, accurate, and kept in sync with the code.
- Justify every significant technical choice (trade-offs, alternatives considered) and get the owner's approval before adopting it. Record accepted decisions in the docs.
- Keep changes reviewable: small, focused, well-explained.
- Git: trunk-based, feature branches and pull requests, Conventional Commits (feat:, fix:, docs:, refactor:, test:, chore:, ci:). Unity project lives in `game/` (ADR 0005). Standards are in ADR 0006; a feature is not done without tests (ADR 0007); CI per ADR 0008.
- Always spell out an acronym the first time it is used in a document or reply (e.g. "Architecture Decision Record (ADR)").

## 2b. Documentation and project management
- **Docs-as-code:** all documentation is Markdown in this repository, which is the single source of truth. Notion is not used (unreliable agent integration).
- Docs layout (create as needed): `docs/specs/` (feature/system specs), `docs/adr/` (Architecture Decision Records, one per significant technical choice), `docs/roadmap.md`, `docs/chat-history.md`.
- Use GitHub tooling for management: Issues for tasks/bugs, milestones and a GitHub Project board for the roadmap/dashboard, pull requests for changes. Use the `gh` CLI or GitHub MCP, and check for existing issues before creating new ones (avoid duplicates).
- Docs must stay portable (validated by the owner): standard Markdown only (relative links, no Obsidian wikilinks or plugin-only syntax), so they render on GitHub and in Obsidian. Diagrams use Mermaid code blocks.
- Obsidian may be used by the owner as a read-only-style viewer on `docs/`; it is not a dependency. `.obsidian/` must be git-ignored.

## 3. Mandatory: chat history logging
- Append every exchange (user prompt + assistant answer) to [docs/chat-history.md](docs/chat-history.md) **before ending the turn**.
- Append only; never rewrite or delete past entries.
- Entry format:

```
## [N] YYYY-MM-DD HH:MM — <short title>
**User:** <verbatim or faithful prompt>
**Assistant:** <summary of answer + actions taken>
**Files changed:** <list or "none">
**Decisions / context for future agents:** <bullets>
```

## 4. Mandatory: maintaining this file
- Whenever the owner gives a general rule, preference, convention, or project-wide decision (anything that should outlive the current chat), add or update it in this file **in the same turn**, in the appropriate section. Do not leave it only in the chat history.
- Keep this file concise, deduplicated, and organized. Edit existing rules rather than appending contradictions; if a new rule supersedes an old one, replace it.
- Note in the chat-history entry when AGENTS.md was updated.
