# Candidate-blind repository-total source review

## Status

**All six source-backed aggregate assessments are frozen before any EffortHours
output is opened for a cohort source.** This checkpoint contains no seed estimate,
manual-QA candidate estimate, estimator comparison, candidate decision, validation
access, or test access.

Artifact `repository-total-source-review/1.0.0` completes the review authorized by
the immutable `repository-total-assessment-cohort/1.0.0` freeze in
[`2.2.0`](../2.2.0/README.md). The shipped `seed-rules/0.4.0` estimator and
development-only `manual-qa-coding-ratio/0.1.0` candidate remain unchanged.

## Reviewed totals

Each range is symmetric around its expected point, while the half-width varies by
case-specific uncertainty. These are credible planning bounds, not formal
probability intervals.

| Case | Disclosure | Confidence | Low | Expected | High | Relative half-width |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| `codex-usage` | Public | Moderate-high | 410 h | 535 h | 660 h | 23.4% |
| `dotnet-image-viewer` | Public | Moderate | 1,900 h | 2,750 h | 3,600 h | 30.9% |
| `nebreck` | Public | Moderate-high | 4,380 h | 5,730 h | 7,080 h | 23.6% |
| `void-harvest-game` | Public | Moderate-low | 1,540 h | 2,420 h | 3,300 h | 36.4% |
| `private-product-site-a` | Private/anonymized | High | 400 h | 500 h | 600 h | 20.0% |
| `private-desktop-utility-a` | Private/anonymized | Moderate-high | 1,465 h | 1,965 h | 2,465 h | 25.4% |

### `codex-usage`

| Material work area | Public evidence | Low | Expected | High |
| --- | --- | ---: | ---: | ---: |
| Authentication and usage-service integration | Core authentication, networking, and parsing | 50 h | 65 h | 80 h |
| History, activity-aware forecasting, and local token evidence | Core history and token subsystems | 70 h | 90 h | 110 h |
| Tray lifecycle, polling, notifications, settings, and startup | Application context and settings | 50 h | 65 h | 80 h |
| Custom popup, chart, icon, theme, hover, and DPI rendering | Application UI | 80 h | 105 h | 130 h |
| Automated tests, build, installation, CI, and documentation | Tests, scripts, workflow, and README | 45 h | 55 h | 65 h |
| Manual validation and hardening | Authentication, refresh, persistence, rendering, DPI, and Windows lifecycle states | 105 h | 140 h | 175 h |
| **Residual** | Small models, formatting helpers, manifests, notices, and bounded glue | **10 h** | **15 h** | **20 h** |
| **Total** | Exact sum | **410 h** | **535 h** | **660 h** |

The main uncertainty is the unstable external service boundary plus representative
Windows credential-file, startup, notification, and DPI combinations.

### `dotnet-image-viewer`

| Material work area | Public evidence | Low | Expected | High |
| --- | --- | ---: | ---: | ---: |
| Filesystem browser, selection, virtualization, thumbnails, and preview state | Browser view models/views, scanner, and cache | 260 h | 360 h | 460 h |
| Image viewing, metadata, slideshow, video playback, tracks, and previews | Viewer, media views, loader, and decoder | 240 h | 340 h | 440 h |
| Editing, safe conversion, crop, resize, watermark, metadata, batch processing, and rename | Editing, batch services, and dialogs | 250 h | 360 h | 470 h |
| File operations, undo, platform trash, duplicates, comparison, and shell integration | File, trash, duplicate, compare, and registration services | 290 h | 420 h | 550 h |
| Cross-platform desktop experience, controls, settings, single instance, and resource monitoring | Controls, application views/view models, and services | 210 h | 300 h | 390 h |
| Automated tests, Native AOT validation, packaging, CI, notices, and documentation | Tests, scripts, packaging, workflows, and README | 180 h | 240 h | 300 h |
| Manual validation and hardening | Operating systems, formats, destructive workflows, large folders, editing, playback, and packaging | 420 h | 650 h | 880 h |
| **Residual** | Small models, converters, metadata helpers, configuration, and bounded glue | **50 h** | **80 h** | **110 h** |
| **Total** | Exact sum | **1,900 h** | **2,750 h** | **3,600 h** |

The native media, shell, trash, filesystem, AOT, and operating-system matrix is
materially larger than its automated fixtures. Destructive-operation safety and
large real media collections remain important uncertainty drivers.

### `nebreck`

| Material work area | Public evidence | Low | Expected | High |
| --- | --- | ---: | ---: | ---: |
| Core loop, input, flight, cameras, expedition lifecycle, persistence, and transitions | Core, game controllers, and chase camera | 430 h | 550 h | 670 h |
| Combat, weapons, projectiles, AI, targeting, ships, capital systems, damage, devices, and pickups | AI, combat, entities, and combat controllers | 540 h | 700 h | 860 h |
| Procedural sectors, planets, caves, bases, asteroids, terrain, collision, navigation, and persistence | World and surface systems | 690 h | 900 h | 1,110 h |
| Procedural meshes/textures, rendering, post-processing, batching, adaptive resolution, FX, debris, shaders, and audio | Rendering, FX, audio, mesh, and shader systems | 610 h | 800 h | 990 h |
| HUD, menus, hangar, touch controls, tutorial, story, quests, trade, inventory, crafting, and progression | UI and gameplay-content systems | 640 h | 850 h | 1,060 h |
| Architecture, smoke, visual, and performance harnesses plus CI, previews, security, and documentation | Tests, workflows, docs, README, and contribution policy | 400 h | 500 h | 600 h |
| Manual gameplay validation, tuning, visual review, device/browser coverage, performance, and hardening | Systems/testing docs and test scenes | 950 h | 1,250 h | 1,550 h |
| **Residual** | Build configuration, notices, small utilities, screenshots, and bounded glue | **120 h** | **180 h** | **240 h** |
| **Total** | Exact sum | **4,380 h** | **5,730 h** | **7,080 h** |

Detailed architecture and deterministic test evidence constrain the estimate, but
gameplay feel, pacing, balance, procedural-art fidelity, input devices, browsers,
and GPUs remain judgment-heavy.

### `void-harvest-game`

| Material work area | Public evidence | Low | Expected | High |
| --- | --- | ---: | ---: | ---: |
| Core loop, session lifecycle, input, camera, persistence, pause/freeze, and co-op | Game/session, input, persistence, camera, and resurrection systems | 190 h | 280 h | 370 h |
| Combat, projectiles, enemies, spawning, skills, leveling, items, stats, and meta-progression | Entities, skills, stats, and progression UI | 260 h | 400 h | 540 h |
| Instanced rendering, animation, collision, targeting, lighting, weather, particles, geometry, textures, and sprites | Rendering, physics, weather, entity-rendering, and procedural-asset systems | 300 h | 470 h | 640 h |
| HUD, menus, level-up/meta screens, custom game, game-over, minimap, overlays, and responsive styling | UI modules, styles, and application HTML | 260 h | 410 h | 560 h |
| Build and documentation | Webpack/Sass configuration, architecture notes, guide, and README | 120 h | 180 h | 240 h |
| Manual gameplay validation, tuning, co-op, browser/input coverage, visual review, scale performance, and hardening | Represented runtime and documented behavior | 360 h | 600 h | 840 h |
| **Residual** | Small configuration, icons, utilities, screenshots, and bounded glue | **50 h** | **80 h** | **110 h** |
| **Total** | Exact sum | **1,540 h** | **2,420 h** | **3,300 h** |

This is the widest relative range. The tracked artifact has no automated test
suite or CI workflow, so runtime behavior, balance, co-op, browser/device variance,
and advertised high-entity performance require more manual inference.

### `private-product-site-a`

| Material work area | Disclosure-safe source review | Low | Expected | High |
| --- | --- | ---: | ---: | ---: |
| Structured product narrative and reference content | Private evidence withheld | 75 h | 90 h | 105 h |
| Web application shell, routing, content architecture, and page composition | Private evidence withheld | 60 h | 70 h | 80 h |
| Responsive visual system, reusable presentation, accessibility, and local assets | Private evidence withheld | 100 h | 120 h | 140 h |
| Automated tests, full coverage gate, linting, formatting, and type safety | Private evidence withheld | 35 h | 40 h | 45 h |
| Production and isolated preview delivery, routing fallback, concurrency safety, and controls | Private evidence withheld | 50 h | 60 h | 70 h |
| Manual validation across routes, layouts, content, accessibility, previews, and production delivery | Private evidence withheld | 75 h | 105 h | 135 h |
| **Residual** | Small configuration, metadata, notices, and bounded glue | **5 h** | **15 h** | **25 h** |
| **Total** | Exact sum | **400 h** | **500 h** | **600 h** |

The small, explicit behavior, test, coverage, and delivery boundary supports the
narrowest range. The private evidence cannot be independently reproduced from the
public artifact.

### `private-desktop-utility-a`

| Material work area | Disclosure-safe source review | Low | Expected | High |
| --- | --- | ---: | ---: | ---: |
| Pure diff, inline comparison, collapse, code analysis, and scanning | Private evidence withheld | 160 h | 210 h | 260 h |
| Repository reading, revision selection, scoped history, attribution summaries, and filesystem fallback | Private evidence withheld | 170 h | 230 h | 290 h |
| CLI, ANSI, JSON, unified-diff input, application launch, and agent protocol server | Private evidence withheld | 190 h | 250 h | 310 h |
| Reusable desktop diff control, syntax, loading, funnel rendering, scrolling, and theming | Private evidence withheld | 210 h | 280 h | 350 h |
| Desktop analysis browser, filters, periods, drill-in layouts, search, worktree controls, and detached views | Private evidence withheld | 210 h | 280 h | 350 h |
| Unit/UI snapshot tests, cross-platform CI, Native AOT packaging, global-tool composition, installers, and docs | Private evidence withheld | 155 h | 200 h | 245 h |
| Manual validation across repositories, revision/worktree states, terminals, JSON, desktop UI, platforms, and packaging | Private evidence withheld | 330 h | 450 h | 570 h |
| **Residual** | Small models, converters, metadata, assets, scripts, and bounded glue | **40 h** | **65 h** | **90 h** |
| **Total** | Exact sum | **1,465 h** | **1,965 h** | **2,465 h** |

Repository topology, terminal behavior, native desktop presentation, and packaging
vary across supported environments. The private evidence cannot be independently
reproduced from the public artifact.

## Review method

The review used only each pinned tracked artifact, its source-backed behavior,
represented tests, documentation, and delivery configuration. It did not execute
target code, install dependencies, inspect Git history, or use historical labor as
an effort signal. No EffortHours estimate or saved report for any case was read.

Material work areas were estimated only where they improved the repository-total
judgment. Small helpers and uncertain minor responsibilities were bounded once in
the residual. Manual validation and hardening remained a visible material area;
its hours were reasoned from the relevant runtime, integration, platform, media,
input, destructive-operation, and delivery surfaces rather than copied from a
fixed candidate ratio.

The public cases retain reproducible repository and object identities from the
cohort freeze. The two private assessments retain only anonymous case IDs and
generic work-area descriptions here. Their exact identities, object IDs, local
paths, and evidence remain in ignored local review records and are not public
calibration evidence.

## Confidence interpretation

Width is deliberately non-uniform:

- the small private product site has the narrowest range because its behavior,
  tests, coverage gate, and delivery boundary are unusually explicit;
- the public game without automated tests has the widest range because runtime
  behavior, balance, co-op interaction, browser variance, and performance require
  more manual inference;
- the larger browser game remains narrower in relative terms because its extensive
  architecture, systems, testing, deterministic smoke, visual, and performance
  evidence constrains the recreation judgment despite its size; and
- cross-platform media, filesystem, terminal, native UI, and packaging boundaries
  widen the desktop application cases according to their actual integration risk.

None of those confidence labels implies empirical production accuracy or
independent review.

## Next boundary

Keep this assessment artifact immutable. A later checkpoint may now produce the
shipped seed and exact manual-QA candidate reports for the six pinned sources and
compare repository low/expected/high totals. It must stop immediately for an
in-range case. A material miss may open only the largest-first discrepancy needed
to explain the decision under `repository-total-materiality/1.0.0`.

The comparison remains development evidence. Test stays sealed, and an advancing
candidate still requires a new finite candidate identity, new admission policy,
and fresh blind validation boundary.
