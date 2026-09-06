# Sortography

Sortography helps you sort a folder of photos into the right folders quickly. Preview each image, send it to a destination with a keyboard shortcut, or drag it where it belongs.

## Download

Download the latest release for your computer from the [Releases](../../releases) page.

- **Windows:** download `Sortography-<version>-Setup.exe` and run the installer. The `win-x64.zip` file is the portable alternative.
- **macOS Intel:** download the `osx-x64.dmg` file, open it, and drag Sortography into Applications.
- **macOS Apple silicon:** download the `osx-arm64.dmg` file, open it, and drag Sortography into Applications.
- **Ubuntu 24.04 x64:** download the `linux-x64.deb` file and open it with your software installer. A `linux-x64.tar.gz` portable archive is also available.

No separate .NET installation is required. For the Windows portable ZIP, extract the entire archive and open `Sortography.exe`. For the Linux portable archive, extract it and run `./Sortography` from that folder; system libraries are still required.

Current releases are unsigned on Windows and not notarized on macOS, so your system may warn or block installation. See the release's installation notes for details.

To update, close Sortography and install the newer version in the same location.

Maintainers: see [Making a release](RELEASING.md) for testing packages and publishing a version.

## Sort Photos

1. Click **Open source** and choose the folder containing the photos you want to sort.
2. Click **Add sorting folder** and choose a destination folder. Add as many destinations as you need.
3. Select a photo in the source list or preview it in the center of the window.
4. Move the photo by clicking a destination card, pressing its shortcut, or dragging the preview onto a destination folder.
5. Continue through the photo list. Sortography moves each file to the selected folder.

You can add existing subfolders as destinations, create or rename subfolders from a folder's context menu, and arrange destination cards by dragging them.

## Helpful Controls

- **Left / Right arrows:** view the previous or next photo.
- **F1 through F9:** move the current photo to the destination assigned to that key.
- **Mouse wheel or `+` / `-`:** zoom the preview.
- **Ctrl+Z** on Windows/Linux or **Cmd+Z** on macOS: undo the latest move.
- **Sort A-Z:** arrange destination cards alphabetically.
- **Clear sorting folders:** remove destinations from the workspace without deleting the folders or their contents.

## Duplicate Files

Use the **On collision** menu to choose what happens when a file with the same name already exists in the destination folder:

- **Keep both:** keep both files with a new name for the moved file.
- **Skip:** leave the source file where it is.
- **Replace:** replace the existing destination file.

Sortography moves files between folders. It does not delete your source folder when you finish sorting.
