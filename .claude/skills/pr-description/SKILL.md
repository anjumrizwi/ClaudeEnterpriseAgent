---
name: pr-description
description: Generate a PR description in Valtech's standard format (Summary, Changes, Test plan) from the current branch's diff and commit history. Use when the user asks to write, draft, or update a pull request description, or to open a PR.
---

# PR Description Generator

Produce a pull request description in this standard format:

```markdown
## Summary
1-3 sentences on *why* this change was made (the problem or goal), not just what changed.

## Changes
- Bullet list of the concrete changes, grouped logically if there are many.
- Focus on what a reviewer needs to know to understand the diff.

## Test plan
- [ ] Bulleted checklist of how this was (or should be) verified.
- Include manual verification steps, tests run, or "no tests added because X".
```

## Steps

1. Determine the base branch (usually `main` or `master`) and confirm the current branch is not the base branch.
2. Gather context in parallel:
   - `git log <base>..HEAD --oneline` — all commits in this branch, not just the latest.
   - `git diff <base>...HEAD` — the full diff.
   - `git status` — check for uncommitted changes that should be flagged to the user.
3. Read the diff and commit messages to understand the *intent* behind the change, not just the mechanical diff. Do not invent rationale that isn't supported by the code or commit messages — if intent is unclear, ask the user or state it as an assumption.
4. Draft the description using the three sections above. Keep the Summary short and focused on why; put mechanical detail in Changes.
5. Treat any client names, credentials, internal URLs, or sensitive data found in commit messages/diffs per the confidentiality rules — mask or generalize unless necessary for the PR.
6. If the user wants the PR actually created or the description applied:
   - Creating a new PR or editing an existing one is a visible, shared-state action — confirm with the user before running `gh pr create` or `gh pr edit`.
   - Use a HEREDOC for the `--body` argument to preserve formatting.
7. Otherwise, just output the drafted description in the chat for the user to copy.
