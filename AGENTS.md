# Agent Instructions — transport-tycoon-2

Standing rules for **every** AI agent/model/chat working on this project. At the start of every session, read this file, [docs/roadmap.md](docs/roadmap.md), and the **current ISO-week** chat-history file under [docs/chat-history/](docs/chat-history/README.md). Do not load the full chat archive by default.

## 1. Project context

- Transport Tycoon-style game. Inspirations: Transport Tycoon Deluxe (TTD) and Rise of Industry (the first one).
- Engine: **Unity 6.3 LTS** (latest 6000.3.x patch; never 6000.5.x; evaluate 6.7 LTS later, ADR 0015), 2D Universal Render Pipeline (URP), UI Toolkit, pure C# deterministic simulation with commands (see [docs/adr/](docs/adr/README.md); ADRs are the source of truth for technical decisions, read them before coding). Other stack details are decided via ADR with owner approval.
- Product discovery decisions are documented in [docs/specs/product-vision.md](docs/specs/product-vision.md), the scope reference (V1/V2/V3+); detailed rules are in [docs/specs/gameplay-systems.md](docs/specs/gameplay-systems.md). Check both before proposing features. Product documents may remain marked Draft pending owner review. All balance values must be data-driven.
- Hard architectural constraints: simulation separated from presentation and deterministic, player actions as commands (future AI opponents in V2, multiplayer in V3+); maps up to 512x512 (sizes S/M/L = 128/256/512); internationalization (i18n) compatible code (English only in V1); robust automated regression tests and continuous integration.
- Owner availability: 20-40 h/week; end of 2026 is an aspiration, not a deadline. Unity version and render pipeline are chosen by the assistant (propose via ADR for approval).
- Targets: Windows PC and WebGL demo.
- Vibe-coded in the sense that the owner steers via chat and AI writes code, but this is a personal project assisted by AI, not a quick delivery.
- Primary purpose: **portfolio** showcasing the owner's skills. Commercial success is secondary.
- Owner: Tech Lead, 15+ years of experience. Very rigorous. Reviews every technical choice and most of the code.
- **Early-collaboration pace (until the owner relaxes it):** for the first issues with a new agent/tooling setup, go slowly and keep autonomy low. Prefer asking before acting when unsure about sequence, decision ownership, or tool use—e.g. whether to do A before B, whether to decide X yourself or ask the owner, and whether a given tool/program/API/action is OK. Optimize for workflow validation and trust calibration over speed.
- **AGENTS.md and chat-history commits:** do not open standalone commits or pull requests solely for `AGENTS.md` or chat-history updates. When those files change, include them in the next issue-linked feature-branch commit/PR.
- **Feature branches:** when starting work on a GitHub issue, create the conventional feature branch from up-to-date `main` without asking (e.g. `feat/<n>-short-slug`). Still ask before merging, force-pushing, or other irreversible git actions.
- **Unity Hub/Editor:** the agent may launch and drive the pinned Unity Editor as needed to generate `.meta` files, scenes, imports, and to run EditMode tests or builds. Prefer doing this in-agent so the owner does not have to open Unity for routine work.

## 2. Quality and working rules

- Apply state-of-the-art coding practices; no quick hacks.
- Documentation must be perfectly ordered, accurate, and kept in sync with the code.
- Justify every significant technical choice (trade-offs, alternatives considered) and get the owner's approval before adopting it. Record accepted decisions in the docs.
- Keep changes reviewable: small, focused, well-explained.
- **Cost-efficient execution without quality trade-offs:** avoid unnecessary model and tool usage, such as repeated CI polling, redundant repository reads, or rerunning checks that a change does not affect. Batch independent reads and related operations, and use completion notifications or a single CI status check when results are needed rather than polling repeatedly. Use economical model settings for routine work and reserve higher-cost models for tasks that benefit from their additional capability. Never reduce code quality, test coverage, security, documentation accuracy, reviewability, or required verification to save cost; optimize redundant overhead only.
- **Unity validation and Actions budget:** never run GameCI automatically on pushes to `main`. Run Unity EditMode tests on pull requests that change `TransportTycoon2/`; keep Windows and WebGL platform builds on-demand only. Run local Unity EditMode tests and Windows/WebGL platform builds at most once per PR, as the final pre-merge validation after the owner authorizes merging—not before creating the PR. Track whether these checks have already run for the PR; if so, do not repeat them when merge approval arrives. Skip local Unity builds for documentation- or workflow-only changes; the independent .NET and Markdown CI checks still run as configured.
- **Proportionate scope:** Before discretionary cleanup or tooling work, weigh measurable expected benefits (performance, shipped size, reliability, maintainability, or security) against implementation/validation time and regression risk. Keep work focused on the requested outcome; do not expand into low-impact cleanup solely for tidiness. Defer or ask before optional work when its benefit is unclear, without skipping required verification.
- **AI art iteration:** If a generated image misses the asset's required composition or shape, revise the prompt and regenerate instead of repeatedly cropping or heavily modifying a poor fit. Use post-processing only for limited, purposeful curation of a suitable result. If curation or import loses clarity, fix that processing step before regenerating. Issue #16 validates ComfyUI feasibility only; do not present its pilot outputs or workflow as production-ready. Workflow quality and in-game art placement remain future work.
- **Unity visual review:** When asked to review Unity scenes or game visuals in a browser, use an actual Unity WebGL build/render where feasible; do not substitute a hand-authored HTML mockup unless the owner explicitly requests one.
- C# language level is **C# 9** (what Unity 6.3 compiles): no file-scoped namespaces, no `init`/records without polyfill, no C# 10+ features in Core. `#nullable enable` at the top of every Core file. Test method names use `Method_Scenario` style.
- Run `dotnet test dotnet/TransportTycoon.Core.Tests` and `dotnet format dotnet/TransportTycoon.Core.Tests --verify-no-changes` before every commit touching C#. Files must use LF line endings (run `dotnet format whitespace` if an editor wrote CRLF).
- Git: trunk-based, feature branches and pull requests, Conventional Commits (feat:, fix:, docs:, refactor:, test:, chore:, ci:). Unity project lives in `TransportTycoon2/` (ADR 0005, 0016). Standards are in ADR 0006; a feature is not done without tests (ADR 0007); CI per ADR 0008.
- Always spell out an acronym the first time it is used in a document or reply (e.g. "Architecture Decision Record (ADR)").

## 2b. Documentation and project management

- **Docs-as-code:** all documentation is Markdown in this repository, which is the single source of truth. Notion is not used (unreliable agent integration).
- Docs layout (create as needed): `docs/specs/` (feature/system specs), `docs/adr/` (Architecture Decision Records, one per significant technical choice), `docs/roadmap.md` (high-level milestones and current next work), `docs/chat-history/` (append-only weekly conversation logs; see that folder’s README).
- Use GitHub tooling for management: Issues for tasks/bugs, milestones and a GitHub Project board for the roadmap/dashboard, pull requests for changes. Use the `gh` CLI or GitHub MCP, and check for existing issues before creating new ones (avoid duplicates).
- **Epic status:** when starting work on a child issue, set both the child issue and its parent epic to In Progress on the Project board; verify both statuses.
- **Issue delivery:** once implementation work on an issue is complete, always commit, push its feature branch, and create a pull request for owner review—do not ask whether to open the PR. Leave the pull request open and wait for owner feedback (changes requested, or explicit go-ahead to merge). Do not merge unless explicitly authorized.
- **Issue closure gate:** never close a GitHub issue or mark its Project item Done while its implementation pull request is still open. Close it only after the PR is merged and the acceptance criteria are verified against the merged result. Prefer the PR's closing keyword to close the issue on merge; keep the board status synchronized. If an issue is closed prematurely, reopen it and restore an active board status immediately.
- **Acceptance checklist:** tick each acceptance-criteria checkbox in the issue body as soon as it is verified (against the merged result), before or when closing the issue. Never close an issue with verified criteria unticked; leave a criterion unticked and say why if it is not yet verified.
- **PR-issue linkage:** every pull request must be created from `.github/pull_request_template.md` and its body must contain a closing keyword for its issue (`Closes #N`) so GitHub links them. Before marking a PR ready or merging, verify `closingIssuesReferences` (e.g. `gh pr view N --json closingIssuesReferences`) lists the issue; fix the body if empty.
- **ADR index integrity:** keep exactly one index row per ADR number, and ensure its title, link, and status match the ADR file. Before finishing an ADR change, search the index for that number and verify it occurs exactly once.
- Docs must stay portable (validated by the owner): standard Markdown only (relative links, no Obsidian wikilinks or plugin-only syntax), so they render on GitHub and in Obsidian. Diagrams use Mermaid code blocks.
- **Markdown verification:** after modifying any Markdown content—including GitHub issue or pull request descriptions—inspect the resulting text to confirm paragraphs, headings, lists, and checkboxes retain their intended structure. Before finishing a change that touches tracked `.md` files, ensure the CI **Markdown lint and link check** job would pass: run the same tools locally when available (`markdownlint-cli2` with `.markdownlint-cli2.jsonc`, and `lychee` with `.lychee.toml`, over `git ls-files -- '*.md' '*.markdown'`), or otherwise verify relative links and lint rules carefully against those configs. Do not push Markdown that breaks that job. For GitHub issue/PR bodies, retrieve the saved body and verify line breaks and checklist formatting rather than trusting the submitted text.
- GitHub account/repo: `sekoya-solutions/transport-tycoon-2` (private for now). Labels (type/area/version) and milestones M0-M6 exist; use them on every issue. Use `gh` (`C:\Program Files\GitHub CLI\gh.exe` if PATH is stale). Branch protection is not available on private free repos: re-check when public or upgraded.
- Never hand-write Unity-generated files (ProjectSettings, Packages/manifest, .meta); let the Unity editor create them.
- Git on GitHub is the ONLY version control system. Never enable Unity Version Control (Plastic SCM) or any other VCS for the project.
- Obsidian may be used by the owner as a read-only-style viewer on `docs/`; it is not a dependency. `.obsidian/` must be git-ignored.

## 3. Mandatory: chat history logging

- Append every exchange (user prompt + assistant answer) to the **current ISO-week** file under [docs/chat-history/](docs/chat-history/README.md) **before ending the turn** (create the week file if it does not exist).
- Append only; never rewrite or delete past entries. Do not append to [archive/vscode-era.md](docs/chat-history/archive/vscode-era.md) or to closed week files.
- Keep a single global entry counter across week files (continues after the archive).
- Entry format:

```text
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
