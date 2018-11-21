Filebert is a Windows .NET 4 program that performs media file diagnostics.

Filebert is a standalone, portable executable that requires .NET 4 for usage.
An installer is provided although Filebert may be installed just by copying the `.exe` file.
The installer makes no registry changes other than what is needed for basic installation.
Settings are configured by supplying command-line arguments.

A console version of this program is also supplied for advanced users.
The console version allows batch operation, logging, and cross-platform usage.
Otherwise, its behavior is consistent with the GUI version.

Filebert is open-source freeware with the complete source at this repository.

## [** DOWNLOAD **](https://github.com/kaosborn/Filebert/releases/)

## [** DOCUMENTATION **](https://github.com/kaosborn/Filebert/wiki/)

## Repository layout

This repository is organized as a single Visual Studio solution with additional documentation files in the root.
These are the top-level folders:

* `ConDiags` - The console front-end branded as Filebert.
* `Harness400` - The domain compiled for test and development only.
* `Install` - Builds `UperApps.msi` installer for all tools.
* `Source` - All the "business" logic of file formats organized into shared libraries.
* `TestFull` - A woefully short set of unit tests. Code coverage is pitiful.
* `WpfDiags` - GUI front-end of Filebert.

For additional developer notes, see:

https://github.com/kaosborn/Filebert/wiki/Developer-notes
