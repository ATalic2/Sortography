# Sortography

A fast, cross-platform photo sorter built with C#, .NET 9, and Avalonia 12. Put every photo in its place using destination shortcuts or drag and drop.

## Run

```cmd
dotnet run
```

## Build

```cmd
dotnet build -c Release
```

## Publish

Publish for the desired platform on that platform:

```cmd
dotnet publish -c Release -r win-x64 --self-contained true
dotnet publish -c Release -r linux-x64 --self-contained true
dotnet publish -c Release -r osx-x64 --self-contained true
dotnet publish -c Release -r osx-arm64 --self-contained true
```

The UI and filesystem services are cross-platform. Native packages should be produced on the corresponding operating system for final distribution and testing.

## Release

Push a semantic-version tag to build and publish a GitHub Release for Windows, Linux, and macOS:

```bash
git tag v1.0.0
git push origin v1.0.0
```

The tag supplies the application version. For example, `v1.2.3` produces binaries with version `1.2.3` and release packages named `Sortography-1.2.3-<runtime>`.

## Controls

- Left / Right arrow: previous or next image, regardless of which main-window panel is focused
- Mouse wheel or `+` / `-`: zoom
- `F1` through `F9`: move to the assigned sorting folder
- `Ctrl+Z` / `Cmd+Z`: undo the latest move
- Drag the displayed image onto a sorting card or destination-tree folder
- Click a card badge to assign or remove its shortcut
- Drag the three splitters to resize the left, right, and bottom work areas
