# OpenCode Dev Container integration

Goal: run the published private OpenCode 2 Feature side-by-side with the
existing Copilot CLI in RockULA! without requiring VS Code to remain open.

## Scope and decisions
- Remote branch: `feature/opencode-integration`, merged to `main`.
- Preserve the project's Windows worktree-parent bind mount
  (`R:\repos\rockula` -> `/repos`) and `workspaceFolder: /repos/rockula`.
- Preserve the locally tested Copilot CLI persistent volume and its directory.
- Use private GHCR Feature `0.1.0` and pin OpenCode binary `2.1.1`.
- Add three separate OpenCode volumes: data, config, state.
- Use `shutdownAction: none`. Disable `updateRemoteUserUID` only for this
  existing RockULA installation to keep the verified numeric UID/GID 1655:1655.
  New projects must measure their own UID/GID and should not copy these IDs.
- No server, HTTP listener, exposed port,
  Docker socket, or provider credentials are configured here.
- Existing repository-specific GitHub MCP entry was removed in prior local
  edits; this branch follows those edits. It does not silently reintroduce it.
- Untracked `.vscode/settings.json` and `.devcontainer/devcontainer-lock.json`
  from the local machine are not committed into this branch.

## Local sync warning
There is already a **local branch of the same name with uncommitted edits**
in `.devcontainer/Dockerfile` and `.devcontainer/devcontainer.json`, plus
untracked files. Do **not** run a blind `git checkout -f`, `git reset --hard`,
`git clean`, or `git pull` until the worktree is saved.

A safe approach is to stash local changes **including untracked files**,
fetch the new remote branch, then fast-forward the existing local branch.
Keep the stash as a backup; do not automatically re-apply its old
Dockerfile/devcontainer changes over the new versions. Recover desired
untracked local settings separately after verifying status.

## Acceptance checks (when the user is at the PC)
1. From a normal Windows session: private GHCR manifest reads successfully
   through Docker's saved credentials; this has already been verified.
2. Verify local `git status`, back up existing changes, then obtain branch.
3. Review changes before rebuilding; confirm the Copilot CLI feature and
   workspace mount are unchanged. This branch renames the container user
   from `vscode` to `developer`, explicitly setting Linux UID:GID `1655:1655` to match the existing
   persistent volumes (verified by `docker exec ... ls -ldn`). It also moves the .agents bind target to
   `/home/developer/.agents`; the Windows source remains the same.
4. Rebuild only RockULA!'s Dev Container. Verify with:
   `devcontainer.cmd exec --workspace-folder "R:\repos\rockula\rockula" -- opencode --version`
5. Confirm `developer` reports UID:GID 1655:1655 and can write all three
   OpenCode mountpoints and the Copilot volume without changing volume ownership.
   Copilot itself should still work.
6. Restart without VS Code and check persistent data; do **not** infer
   automatic startup from `shutdownAction` alone.
7. Configure DeepSeek through the existing local Gateway, MCP constraints,
   and authenticated remote access in later reviewed steps.

The initial `vscode` container was built and OpenCode 2.1.1 ran successfully,
with the existing named volumes attached. The `developer` migration has **not**
yet been rebuilt or tested on the home machine. The existing project-specific AGENTS.md
rules and test protocol remain in force.
