# Roadmap

Build order for Shelvd Web, in dependency sequence — each milestone assumes the
ones above it are done. Update statuses as work lands; this is a living doc,
not a spec frozen at time of writing.

1. **Project structure + CI** — multi-project solution, GitHub Actions, CLAUDE.md ✅ Done
2. **Auth** — Supabase login/logout, protected routes ✅ Done
3. **Core data flow** — fetch and display book library from Supabase ⬜ Not started
4. **Library browsing** — search, filter, sort, shelf views ⬜ Not started
5. **Public profiles** — shareable URLs (SEO prerendering shines here) ⬜ Not started
6. **Polish** — responsive design, loading states, error handling ⬜ Not started
7. **Tests** — service layer unit tests, key component tests ⬜ Ongoing — auth is covered (`Shelvd.Web.Tests`); extend as each milestone above lands

## Notes

- Milestone 7 isn't a final phase tacked on at the end — tests are written
  alongside each milestone (TDD per CLAUDE.md conventions). It's listed last
  here because it's also worth a standalone pass once the UI stabilizes, to
  fill gaps.
- Realtime (direct WASM→Supabase subscriptions) isn't its own milestone above;
  it's a carve-out to reintroduce once milestone 3/4 exist. See the
  `project-realtime-carveout-decision` memory for the recorded decision.
