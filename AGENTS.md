# AGENTS.md

## Packaging Shortcut

When the user says `package:` for this repository, execute the following steps in order without asking for confirmation prompts for the batch:

1. Commit and push the current changes in `C:\Users\thendar\CCM-Desktop-Companion.git`.
2. Rebuild the MSI using `tools\Build-DesktopCompanionMsi.ps1`.
3. Update the GitHub release asset by replacing the existing MSI asset on the current release tag with the newly built MSI.

## Notes

- Treat `package:` as a direct action command, not a planning request.
- If the working tree is clean, still rebuild the MSI and refresh the GitHub release asset unless the user says otherwise.
- Do not ask for yes/no confirmations for the packaging batch.
- Keep all desktop companion work in this external repository, not in production addon directories.
