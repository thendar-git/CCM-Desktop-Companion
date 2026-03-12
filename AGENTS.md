# AGENTS.md

## Packaging Shortcut

When the user says `package:` for this repository, execute the following steps in order without asking for confirmation prompts for the batch:

1. Commit and push the current changes in `C:\Users\thendar\CCM-Desktop-Companion.git`.
2. Run `tools\Publish-DesktopCompanionRelease.ps1` to rebuild the MSI, create or update the git tag for the current version in `CCM.DesktopCompanion.csproj`, and publish the matching GitHub release asset.

## Notes

- Treat `package:` as a direct action command, not a planning request.
- The release version is always derived from `CCM.DesktopCompanion\CCM.DesktopCompanion.csproj`.
- If the working tree is clean, still run `tools\Publish-DesktopCompanionRelease.ps1` unless the user says otherwise.
- Do not ask for yes/no confirmations for the packaging batch.
- Keep all desktop companion work in this external repository, not in production addon directories.
