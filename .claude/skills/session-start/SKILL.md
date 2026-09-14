---
name: session-start
description: What to do when a SESSION-NN-*.md prompt is pasted for Till Winter. Use at the start of every session before touching code.
---

# Session start

1. `CLAUDE.md` is already loaded; do not re-read it.
2. Read the session prompt once. Note its numbered steps and the GDD section numbers it cites.
3. Read only those GDD sections: `Grep` for `^## <n>\.` in `docs/GDD.md` to find the line, then `Read` with offset/limit for that section. Never read the GDD entirely.
4. Read only the last section of `DECISIONS.md` (grep for the last `^# ` heading, read from there).
5. Ask `repo-scout` for the files and line ranges each step touches (one question per step, batched in one Agent call if independent).
6. Propose the plan to the user in ≤ 15 lines: branch name, commits in order, tests to add, docs to touch, open questions. Proceed unless a step needs a decision only the developer can make.
7. Implement with targeted edits. Tests go through `test-runner`; docs through `docs-writer`; PR via the `open-pr` skill.
8. When the prompt and the GDD disagree, the GDD wins; note the conflict for `DECISIONS.md`.
