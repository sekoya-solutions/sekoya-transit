# ADR 0011: Localization

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
The code base must be internationalization (i18n) compatible; only English is shipped in V1 ([product-vision.md](../specs/product-vision.md)). The UI uses UI Toolkit ([ADR 0004](0004-code-organization-and-ui.md)), and WebGL is a target.

## Options considered
1. **Unity Localization package** (String Tables, locale management, UI Toolkit support).
2. Own lightweight key-value JSON system: fewer dependencies and simpler WebGL behaviour, but tooling must be built.
3. Hard-coded strings: rejected.

## Decision
Option 1:
- All user-facing text is referenced by **key**; no hard-coded display strings (checked in review and by a CI search).
- **Core never produces display text**: it exposes identifiers and values; the UI layer formats them.
- Content files reference localization keys, not text ([ADR 0010](0010-data-driven-content-format.md)).
- Numbers, dates, and currency are formatted through culture-aware helpers.
- English only in V1.

## Consequences
- The package's behaviour on WebGL and with UI Toolkit must be verified in the early prototype; if blocking, fall back to option 2 via a new ADR.
- Adding a language later requires only new string tables (and font coverage).
