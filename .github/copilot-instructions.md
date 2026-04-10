# Workspace Instructions

## Commit Message Convention

When creating git commits in this repository, the first line of the commit message must start with:

`<symbol> <TYPE>: <short summary>`

Rules:
- `<symbol>` must be one of `+`, `-`, or `~`.
- `<TYPE>` must be a 3-letter uppercase code describing the change kind.
- The short summary must be concise and written in English.

Supported type codes:
- `ADD` for new files, features, or capabilities
- `DEL` for removals or deletions
- `FIX` for bug fixes or corrections
- `IMP` for improvements, refactors, or optimizations
- `REV` for revisions, rewrites, or notable rework

Recommended symbol mapping:
- `+` with `ADD`
- `-` with `DEL`
- `~` with `FIX`, `IMP`, or `REV`

Examples:
- `+ ADD: create initial container build prompt`
- `- DEL: remove obsolete workflow draft`
- `~ FIX: correct README prompt link`
- `~ IMP: streamline container tagging script`
- `~ REV: rewrite CI scaffold prompt`

Do not create commit titles that do not follow this format.

## Branch Naming Convention

Use these branch prefixes:
- `feature/` for new features and larger planned work
- `fix/` for bug fixes and corrections

Examples:
- `feature/container-build-system`
- `fix/workflow-permission-issue`

Do not create work branches without one of these prefixes.