# CLAUDE.local.md — template

Copy this file to `CLAUDE.local.md` at the repository root and fill it in. `CLAUDE.local.md` is gitignored, so it stays
on your machine. Claude Code loads it after `CLAUDE.md` (`CLAUDE.md` §0.10). Keep shared rules in `CLAUDE.md` and only
what is yours here.

## Reference sources (`CLAUDE.md` §3.1)

| Role | Location | Notes |
|---|---|---|
| 4.90 source (port source) | `<path>` | The 4.9 CW plugins, `CWPlans/`, `CWDocs/`, `CLAUDE.md`, `tasks/lessons.md`, `Tests/CW.Tests` |
| 4.6 source | `<path>` | `Nop.Plugin.Jim2.*` |
| 4.1 source | `<path>` | `Nop.Plugin.TIG.*` |

If a reference source is inside the repository folder, add it to `.gitignore` and say so here.

## Local environment

- Repository: `<path>`
- Database: `<server>`, catalog `<name>`
- Dev site: `<url>`

## How I work with Claude

- `<what Claude may edit directly without asking (e.g. Tests/CW.Tests, documents), if anything — CLAUDE.md §0.1>`
- `<how you want code changes handed over — format, ordering, line numbers>`
- `<any CLAUDE.md rule you relax for your own sessions, named with its section (e.g. sub-agents, §0.2)>`

## My lessons

`<lessons about how you work with Claude — CLAUDE.md §0.7. Shared lessons go in tasks/lessons.md>`
