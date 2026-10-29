# ADR 0034: WebGL demo on GitHub Pages

- Status: Accepted
- Date: 2026-10-29
- Deciders: Owner

## Context

The portfolio needs a playable browser demo ([product vision](../specs/product-vision.md)). [ADR 0008](0008-continuous-integration.md) left "deploy the WebGL demo to GitHub Pages" for later, with Unity's compression and header handling to verify. GitHub Pages serves static files only: it cannot add the `Content-Encoding` header that Unity's compressed WebGL files normally need. The repository became public on 2026-10-29 (after the rename to Sekoya Transit, Issue #198), which makes Pages and standard GitHub Actions runners free. The existing manual platform build job builds the scenes in Unity's build settings, which only list the template `SampleScene`, not MapPlay. The download budget is 25 MB gzip for V1 ([ADR 0017](0017-map-rendering-approach-and-performance-budgets.md)).

## Options considered

1. **Hosting.** GitHub Pages from this public repository (chosen); a separate public repository holding only the build (keeps the source private, needs a cross-repository token); GitHub Pro to publish from a private repository (monthly cost, limited Actions minutes); itch.io (not tied to the repository).
2. **Where the build runs.** GameCI on GitHub Actions (chosen: reproducible from a commit, reuses the Unity license secrets); a local build uploaded by hand (fast, but the published demo is not traceable to a clean commit).
3. **Compression.** Gzip with decompression fallback (chosen: about four times smaller files, and Unity's loader decompresses them in the browser, so no server header is needed; gzip decompresses quickly in JavaScript); no compression (simplest, much larger download); Brotli with fallback (smallest, but decompressing Brotli in JavaScript slows startup noticeably).
4. **Code optimization.** Runtime speed (chosen: the simulation is CPU-bound on Large maps); disk size with link-time optimization (smaller code, slower to run, longer builds).
5. **Trigger.** Manual dispatch only (the original choice, matching [ADR 0021](0021-unity-ci-minute-budget.md) while the repository was private); version tags (automatic, but every tag costs a full build); (amended 2026-10-29) automatic on pushes to `main` that change the game, plus manual dispatch (chosen: Actions minutes on a public repository are free, which was the reason for the manual-only rule).

## Decision

- The workflow `.github/workflows/deploy-demo.yml` (**Deploy WebGL demo**) runs automatically on pushes to `main` that change `SekoyaTransit/**` or the workflow itself, and on manual dispatch. Its build job checks out with Git LFS, restores a `WebGLDemo` Library cache (falling back to the `WebGL` platform-build cache), and builds with GameCI through `MapPlaySceneBuilder.BuildWebGLDemo`. Its deploy job runs only if the build job succeeded (`needs: build`), so a failed build never replaces the published demo; it publishes the build with `actions/upload-pages-artifact` and `actions/deploy-pages` to the `github-pages` environment. Pages uses the "GitHub Actions" source.
- `BuildWebGLDemo` builds the MapPlay scene as a release player (not Development), gzip with decompression fallback, code optimization Runtime speed, into GameCI's `-customBuildPath` (or `Builds/WebGLDemo` locally). It sets these player settings only for the build and restores them afterwards, so local Development previews stay uncompressed.
- The demo is served at `https://sekoya-solutions.github.io/sekoya-transit/`.

## Consequences

- The live demo follows `main` after every game-affecting merge (a cold-cache build can take up to an hour; runs queue on the `pages` concurrency group rather than cancelling a publish in progress), and each published demo maps to the commit it was built from. A manual run stays available from the Actions tab.
- This WebGL build is the only automatic platform build. The Windows and WebGL matrix in `unity.yml` stays manual dispatch only.
- Size of the first local release build (2026-10-29): 24.4 MiB (25.6 million bytes) in total, of which the gzip WebAssembly code is 16.3 MB and the data file 9.1 MB. That is at the 25 MB budget of ADR 0017 (just under in MiB, just over in decimal megabytes), so code stripping or a disk-size optimization is the first lever if the demo grows.
- With the decompression fallback, the browser decompresses the files itself at startup instead of the network layer; this costs a little startup time but works on any static host.
- The Development-only frame logger and F10 load test are not in the demo.
- Saves use the browser's IndexedDB under the Pages origin, so they persist per browser across visits.
- Follow-up: link the demo from the README ([Issue #72](https://github.com/sekoya-solutions/sekoya-transit/issues/72)).
