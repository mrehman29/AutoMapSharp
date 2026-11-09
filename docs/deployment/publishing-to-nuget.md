# Publishing to NuGet

This page explains how AutoMapSharp packages get from this repository to [nuget.org](https://www.nuget.org).

## What gets published

| Package | Project |
|---|---|
| `AutoMapSharp` | `src/AutoMapSharp` |
| `AutoMapSharp.Extensions.DependencyInjection` | `src/AutoMapSharp.Extensions.DependencyInjection` |
| `AutoMapSharp.Collections` | `src/AutoMapSharp.Collections` |
| `AutoMapSharp.MigrationAnalyzer` | `src/AutoMapSharp.MigrationAnalyzer` |
| `dotnet-automapsharp-migrate` | `src/AutoMapSharp.Cli` |

The source generator and the compile-time analyzers are not separate packages. They ship inside `AutoMapSharp`.

## Where the package information comes from

- **Shared details** are in `Directory.Build.props`: version, authors, license, project URL, icon, release notes and Source Link settings.
- **Package-specific details** are in each project file: the package ID, title, description and tags.
- **The nuget.org page** for each package shows the `README.md` next to its project file. These READMEs use absolute GitHub links, because relative links do not work on nuget.org.
- **The icon** is `assets/icon.png`.
- **Release notes** point to `CHANGELOG.md` and `MIGRATION_GUIDE.md`.

## One-time setup

1. Sign in to [nuget.org](https://www.nuget.org) and open **API Keys**.
2. Create a key with the **Push** scope. Use the glob pattern `AutoMapSharp*`, and add `dotnet-automapsharp-migrate` as a second pattern, or create a second key for it.
3. In the GitHub repository, open **Settings > Secrets and variables > Actions** and add a secret named `NUGET_API_KEY` with the key.
4. Optionally, open **Settings > Environments**, create an environment named `nuget` and add yourself as a required reviewer. Each release then waits for your approval before it pushes anything.

## Releasing a version

1. Update `<Version>` in `Directory.Build.props`.
2. Add a section for the version to `CHANGELOG.md`.
3. Merge to `main`.
4. Tag the commit and push the tag. The tag must be `v` followed by the exact version:

   ```bash
   git tag v1.0.0-preview.1
   git push origin v1.0.0-preview.1
   ```

The **Publish to NuGet** workflow then checks that the tag matches the version, builds, runs the tests, packs and tests the packages, and pushes them. The packages are also attached to the workflow run as an artifact.

New packages usually appear on nuget.org within a few minutes, after NuGet has validated and indexed them.

## Publishing from your own machine

The workflow is the recommended way, because it builds from a clean checkout of a pushed commit, so Source Link points at real source on GitHub. If you need to publish by hand:

1. Copy `nuget-publish.env.example` to `nuget-publish.env` and replace the placeholder with your key. Git ignores `nuget-publish.env`, so the key stays on your machine.
2. Run the script:

   ```bash
   # Build, test and pack, without pushing anything
   ./scripts/publish-nuget.sh --dry-run

   # Build, test, pack and push
   ./scripts/publish-nuget.sh
   ```

The script can also read the key from the `NUGET_API_KEY` environment variable. It refuses to push while the key is still the placeholder, when `nuget-publish.env` is tracked by git, or when there are uncommitted changes. It uses `--skip-duplicate`, so running it again for a version that is already published does nothing harmful.

## Keeping secrets out of the repository

This repository is public. Never commit an API key, certificate or password.

- **API keys** go in `nuget-publish.env` locally, or in GitHub Actions secrets for the workflow.
- **`.gitignore`** blocks `*.env`, `.env.*`, `*.pfx`, `*.p12`, `*.snk`, `*.key`, `*.pem`, `secrets.json` and local appsettings files.
- **If a key leaks,** delete it on nuget.org straight away and create a new one. Removing it from git history does not make it safe again.

## Things to know

- **A published version cannot be replaced.** You can unlist it on nuget.org, but the same version number can never be pushed again. Fix problems by releasing a new version.
- **Preview versions** such as `1.0.0-preview.1` only show up when users tick "Include prerelease" or pass `--prerelease`.
- **Check the packages locally first.** `./scripts/pack-and-test.sh` puts them in `artifacts/packages`. A `.nupkg` is a zip file, so you can open it to check its contents, or view it in [NuGet Package Explorer](https://github.com/NuGetPackageExplorer/NuGetPackageExplorer).
