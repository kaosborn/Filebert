![logo](Images/FirstAidWin256.png)
# Filebert

[![Test](https://github.com/kaosborn/Filebert/actions/workflows/Test.yml/badge.svg)](https://github.com/kaosborn/Filebert/actions/workflows/test.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/kaosborn/Filebert/blob/master/LICENSE)

Filebert is a .NET 4.8 program that performs media file diagnostics.
Windows XP thru 11 are supported with limited support for other operating systems.

An installer is provided although a portable install may be performed by copying either of the standalone `.exe` files:
* `filebert.exe` - console interface
* `filebertWin.exe` - GUI interface

The installer makes no registry changes other than what is needed for basic installation.
Full functionality requires that `flac.exe` is available to the command line.

A console version of Filebert is available as well as a Windows GUI version.
The console version allows batch operation, advanced logging, and cross-platform use.
The behavior of the two executables is identical since they reference the same domain codebase.

Filebert is freeware with complete source and build available for inspection at GitHub.

## [** DOWNLOAD **](https://github.com/kaosborn/Filebert/releases/)

## [** DOCUMENTATION **](https://github.com/kaosborn/Filebert/wiki/)

## Repository layout

This Git repository is organized as a single Visual Studio solution plus some accessories in the root.
These are the solution's projects:

* `Source` - Codebase in shared projects by namespace.
* `ConDiags` - Builds the console executable. Architecture is MVC.
* `Harness480` - Builds the domain. For test and development only.
* `Test480` - MSTest unit tests with mock. Code coverage is pitiful.
* `WpfDiags` - Builds the WPF executable. Architecture is MVVM.
* `Install` - Builds the entirely optional installer.

## Minimum build requirements

* Visual Studio Community 2022. Earlier versions might work too.
* Microsoft Visual Studio Installer Projects extension.
* Release configuration, F6 key.

Developer notes at:

https://github.com/kaosborn/Filebert/wiki/Developer-notes
