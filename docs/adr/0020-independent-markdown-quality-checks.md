# ADR 0020: Independent Markdown quality checks

- Status: Proposed
- Date: 2026-10-20
- Deciders: Owner

## Context

[ADR 0008](0008-continuous-integration.md) places Markdown lint and link checks in the fast plain-.NET job. Issue #17 requires Markdown validation to run independently for every pull request and push to `main`, and to cover all tracked project Markdown.

## Options considered

1. **Add a separate job to the existing CI workflow:** shares triggers and permissions while isolating Markdown feedback from .NET and Unity checks.
2. Keep Markdown checks in the .NET job: simpler workflow, but Markdown validation depends on the .NET job and its outcome.
3. Create a separate workflow: independent execution, but duplicates triggers and workflow configuration.

## Decision

Proposed: add an independent Markdown quality job to the existing CI workflow.

- Use markdownlint-cli2 for style checks and lychee for local and external link checks, including local anchor fragments.
- Collect only tracked Markdown paths with `git ls-files -- '*.md' '*.markdown'`.
- Run on pull requests and pushes to `main`, with no dependency on the .NET or Unity jobs.
- Pin GitHub Actions to immutable release commits and pin the lychee binary version.
- Keep necessary lint exceptions narrow: no project line-width convention exists; structured GitHub templates have established formats; accepted ADRs must not be reformatted.
- Exclude private GitHub repository/project links and the owner-local WebGL preview from external requests. Continue checking local links and fragments.

The proposed workflow and configurations are documented in [ci.yml](../../.github/workflows/ci.yml), [markdownlint configuration](../../.markdownlint-cli2.jsonc), and [lychee configuration](../../.lychee.toml). Lychee's `anchor-only` fragment mode checks local Markdown headings without attempting text-fragment resolution. Locally, run `markdownlint-cli2 --config .markdownlint-cli2.jsonc` and `lychee --verbose --no-progress --config .lychee.toml`, passing each command the tracked Markdown paths reported by `git ls-files -- '*.md' '*.markdown'`.

## Consequences

- Markdown checks report independently and do not consume Unity runner time.
- The initial lint baseline requires whitespace/table-format fixes in non-ADR documentation; accepted ADRs remain unchanged.
- Private and owner-local external URLs cannot be verified by CI, but relative links and fragments remain in scope.
- This decision remains proposed pending owner approval.
