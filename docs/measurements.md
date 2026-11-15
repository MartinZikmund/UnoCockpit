# Measurements

Cold load of the deployed site (cache disabled, Playwright Chromium on a desktop over a wired connection).

| Date | Build | Page | Transferred | Ready (JS bridge + first view) |
|---|---|---|---|---|
| 2026-11-15 | Release, interpreter, Uno.Sdk 7.0.22 | `/?spike` | 16.9 MB | 4.9 s |
| 2026-11-15 | Release, interpreter, InvariantGlobalization | `/` (full app) | 16.85 MB | 4.3 s |

## What the first load is made of (gzip as served by GitHub Pages)

The biggest pieces, uncompressed: `dotnet.native.wasm` 10.8 MB (runtime + Skia), `Uno.UI` 9.7 MB, the app assembly 6.1 MB (5.4 MB of it is ICU data that Uno's Skia text layout embeds), `Uno.UI.FluentTheme` 3.8 MB, `System.Private.CoreLib` 1.9 MB, and fonts (Inter ×3, Open Sans, Fluent icons) about 2.5 MB.

Things tried:
- `InvariantGlobalization` removes .NET's own ICU files (about 2.5 MB raw). Kept.
- `PublishTrimmed` + `TrimMode=partial` crashes Uno 7.0 at startup (invalid vtable slot in `WebAssemblyWindowWrapper`). Not used.
- AOT was not measured: it grows the download, which is already the tightest constraint.

The 12 MB budget isn't met yet. The remaining big wins depend on Uno (trimming support, a smaller ICU data set), or on hosting with Brotli instead of GitHub Pages' gzip.
