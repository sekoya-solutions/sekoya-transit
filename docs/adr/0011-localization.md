# ADR 0011: Localization

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
The code base must be internationalization (i18n) compatible; only English is shipped in V1, and French is added in V2 ([product-vision.md](../specs/product-vision.md)). The UI uses UI Toolkit ([ADR 0004](0004-code-organization-and-ui.md)), and WebGL is a target.

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
- English only in V1. V2 ships English and French (Issue #338); see "Shipped languages" below.

## Shipped languages (Issue #338)
- The `GameUi` string table has an English and a French table. Both texts of every key are written side by side in `GameUiBootstrap`, which also removes any key no longer listed. French texts use non-breaking spaces before `: ; ! ? %` and inside « », added when the table is written. French falls back to English for a missing text.
- English and French are the only locales; the project holds no other Locale asset (Issue #330).
- Startup language, in order: a `-language=` command line argument (development), then the language the player chose on the start menu, then the system language (Windows) or browser language (WebGL) when it is English or French, then English.
- The start menu has one button per language, each named in its own language. The choice is stored in PlayerPrefs (the registry on Windows, IndexedDB on WebGL) and saved at once, outside the save file. The startup locale is never stored, so a player who never chose keeps following the system language.
- A text lookup that finishes after the player switched language is redone in the new language, so an old-language text never overwrites a newer one.
- Numbers follow the selected language (for example `1,234` in English and `1 234` in French). Game dates are shown as `DD/MM/YYYY` in French and `YYYY-MM-DD` in English (owner decision); the format is chosen by language code, not from the culture's own date pattern, so English never takes the United States `M/d/yyyy` order. File names keep `YYYY-MM-DD`. Town names are proper names from the content data and are not translated.

## Consequences
- The package's behaviour on WebGL and with UI Toolkit must be verified in the early prototype; if blocking, fall back to option 2 via a new ADR.
- Adding a language later requires only new string tables (and font coverage).
- String lookups are asynchronous, so every shell text request goes through `ShellTextRequests`, which counts the running requests. The shell stays hidden until the load-time requests have finished, with a safety timeout of 10 seconds of running time, not counting blocked frames (Issue #301); a test fails if a script calls the string database directly.
