# Validation — EXP-only 1.0.3

Checked October 26, 2026 on Windows x64.

- Compiled with the .NET Framework C# compiler, with no build warnings.
- 66 automated checks passed: EXP table landmarks, parser validation and fallback, baseline/loss confirmation, level-up accounting, rate warmup, pause/gap rebasing, ETA, eight-hour simulated sample retention, both layouts at multiple scales, stable 12-second display snapshots and the mode button's scaled hit area.
- The unchanged OCR reader previously passed 15 additional checks across three recorded gameplay screenshots at levels 41 and 42: full-frame read, bottom-30% read (the actual capture region), cached read, rejection of a covered HUD and recovery when it reappears.
- Full (660 × 170), tiny (480 × 38), 75% full and 150% tiny renders inspected. Images in this repository contain synthetic data and original code-drawn UI only.
- Rounded window regions replace magenta color-key transparency. Layered opacity remains fully opaque to preserve the existing click-through option. The full level badge has more right padding.
- The tiny-view chevron sits at the far right in a dedicated clickable area, with the ETA and pause indicator kept clear of it. The full-view chevron stays at the top center. Both retain a hover highlight, tooltip and keyboard shortcut; this release makes no changes to EXP reading or refresh timing.
- Source and release were packaged from an explicit EXP-only file list. The executable contains only the standalone tracker, EXP calculation/parser, OCR, overlay, preferences and window-capture code. No private settings, session data, gameplay screenshots or other app modules are distributed.

This is offline and recorded-frame validation. This clean edition has not been observed during a live training session, and multi-monitor dragging / global shortcuts have not received an interactive live-game trial. The source includes the build and test entry points so maintainers can reproduce the automated checks. No claims of publisher approval or ban safety are made.
