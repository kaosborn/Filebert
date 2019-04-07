![logo](Images/FirstAidWin256.png)
# Filebert

Filebert is a Windows .NET 4.0 program that performs media file diagnostics.
Windows XP thru 10 are supported with limited support for other operating systems.

An installer is provided although a portable install may be performed by copying either of the standalone `.exe` files.
The installer makes no registry changes other than what is needed for basic installation.
Settings are configured by supplying command-line arguments only.
Full functionality requires that `flac.exe` is available to the command line.

A console version of Filebert is available as well as a windowed version.
The console version allows batch operation, advanced logging, and cross-platform usage.
Otherwise, its behavior is consistent with the windowed version.

Filebert is freeware with complete source available for inspection at GitHub.

## [** DOWNLOAD **](https://github.com/kaosborn/Filebert/releases/)

## [** DOCUMENTATION **](https://github.com/kaosborn/Filebert/wiki/)

## Repository layout

This Git repository is organized as a single Visual Studio solution with additional documentation files in the root.
These are the top-level folders:

* `ConDiags` - Builds the console front end.
* `Harness400` - Builds the domain. For test and development only.
* `Install` - Builds the `Filebert.msi` installer.
* `Source` - Shared projects by namespace.
* `TestFull` - MSTest unit tests with mock. Code coverage is pitiful.
* `WpfDiags` - Builds the WPF front end.

## Build requirements

* Visual Studio Community 2019 (or 2017) including .NET Framework 4 tools.
* Microsoft Visual Studio Installer Projects extension.
* Release configuration, F6 key.

For additional developer notes:

https://github.com/kaosborn/Filebert/wiki/Developer-notes
