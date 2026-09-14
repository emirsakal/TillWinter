---
name: open-pr
description: How a Till Winter feature branch is finished and turned into a pull request (commits, docs, PR body, no merge).
---

# Finishing a session and opening the PR

1. Branch was created from `main` at session start: `feat/<topic>` (or `chore/<topic>`). Commits are small, `type: summary` style, and each ends with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
2. Before the PR: tests green via `test-runner`; smoke test green if the Unity layer changed.
3. Docs via `docs-writer`, one call with a bullet list: `DECISIONS.md` (new section named after the session), `CLAUDE.md` only if an architecture rule changed, `README.md` if controls/tests changed, `docs/GDD.md` only for rules implementation forced (marked `*(vX.Y)*`).
4. Push: `git push -u origin <branch>`.
5. PR body (write to a scratch file, then `gh pr create --base main --head <branch> --title "<type>: <title>" --body-file <file>`):
   - Summary bullets, Tests (passed/total, what is new), GDD changes list, "What to look at when playing".
   - End with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
6. Do not merge. Report the PR URL and a three-line "what the developer should feel when playing".
7. Merge, when asked: `gh pr merge <n> --squash --delete-branch`, then `git checkout main && git pull`.
