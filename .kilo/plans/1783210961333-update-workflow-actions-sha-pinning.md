# Update GitHub Actions to latest versions + SHA pinning

**Target file:** `.github/workflows/build-and-package.yml`

## Pin format
`uses: <repo>@<sha> # <tag>`

## Changes

| Location | Current | New |
|---|---|---|
| L23, L61, L121 | `actions/checkout@v6`/`v3` | `actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0 # v7.0.0` |
| L28, L66, L126 | `actions/setup-dotnet@v3` | `actions/setup-dotnet@26b0ec14cb23fa6904739307f278c14f94c95bf1 # v5.4.0` |
| L42, L96, L150 | `actions/upload-artifact@v3` | `actions/upload-artifact@bbbca2ddaa5d8feaa63e36b76fdaad77386f024f # v7.0.1` |
| L102–111 | `actions/upload-release-asset@v1` (archived) | Replace with built-in `gh release upload <tag> <glob> --clobber` (`GH_TOKEN` env) |

## Notes / risks
- `upload-artifact@v3` is disabled by GitHub, so this update is required for the workflow to run.
- Matrix artifact names include `${{ matrix.runtime }}` (unique) — satisfies v4+ uniqueness requirement.
- Release asset filename already embeds the tag version (from "Create portable package"), so `gh release upload` naming stays consistent.

## Optional follow-up
Add `dependabot.yml` (`github-actions` ecosystem) so SHA-pinned actions auto-update; otherwise SHAs go stale.

## Validation
- Parse YAML after edits.
- Confirm each `uses:` resolves to the listed SHA/tag.
