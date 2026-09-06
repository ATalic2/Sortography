# Making a release

The Release workflow builds self-contained packages for Windows x64, macOS Intel and Apple silicon, and Ubuntu 24.04 x64. It runs the tests on each build host before packaging. All four builds must succeed before the GitHub Release is created.

## Try the packages first

Push your changes, open **Actions > Release > Run workflow**, and select the branch to test. Manual runs produce downloadable Actions artifacts with a `0.0.0-preview.<run number>` version and never publish a GitHub Release. Extract the Actions artifact to get the installer inside.

Install and launch each package on its target system. Check that you can open a photo folder, preview a photo, move it, and undo the move. On Windows, also check installing over an older version and uninstalling. Local builds and unit tests do not replace these checks.

## Publish

From the tested commit:

```sh
git tag v1.0.0
git push origin v1.0.0
```

Use a new `vMAJOR.MINOR.PATCH` tag for each release, or a prerelease such as `v1.1.0-beta.1`. The tag sets the application and installer versions. Prerelease tags produce GitHub prereleases. Do not reuse a published version. The repository's Actions settings must allow the workflow's `contents: write` permission to create releases.

The workflow attaches the Windows installer and ZIP, two macOS DMGs, the Linux DEB and archive, and SHA-256 checksums. Download instructions from `installer/release-notes.md` precede the generated change notes.

## Signing and support

Windows installers are unsigned. macOS apps are ad-hoc signed, but are not Developer ID signed or notarized. A release suitable for frictionless public installation still needs publisher signing certificates and macOS notarization. No signing secrets are currently required or configured.

Linux packages target Ubuntu 24.04; other distributions are not verified. Self-contained publishing includes .NET, but not operating-system libraries. The macOS builds are cross-published on the available macOS runner; test both Intel and Apple silicon hardware before advertising support.

The project currently targets .NET 9. Review the supported .NET baseline before a public release.
