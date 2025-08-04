# EmuLibrary

EmuLibrary is a library extension for [Playnite](https://www.playnite.link), an open source video game library manager, focused on emulator ROM management.

While Playnite has had built-in support for scanning paths for installed ROMs and adding them to the library since version 9, EmuLibrary provides alternate functionality.

EmuLibrary treats one or more folders of ROMs/Disc images as a library from which you can "install" games. It can be useful if you have a large collection of emulated games and limited storage where you play them versus where you store them (HTPC vs. NAS, for example). It also is useful for keeping the list of emulated games up to date, and for being able to filter via installed/uninstalled.

## About this fork

This is a fork of the original Emulibrary extensions, created by [@psychonic](https://github.com/psychonic/Playnite-EmuLibrary).

I have made some changes to the original code to adapt them to my specific needs, as the original extension did not fit my workflow. Take into account that this fork is not intended to be a general-purpose solution, but rather a personal adaptation of the original extension. However, I believe that some of the changes I have made may be useful to others, so I am sharing this fork in the hope that it can be helpful to those who need it.

### Changes

Added:

* Showed file path even when the game is not installed.
* Added an option to show the file name instead of the full path.
* Added a new file type that uses M3U files to determine the files to copy for multi-file games in subfolders.
* Added an option to automatically remove installed games missing source files.

Removed:

* Yuzu file type.
* Legacy plugin support.

## Setup

To set it up, you create mappings to combine one of each of the following:

* Emulator - either a built-in emulator or a custom emulator manually added
* Emulator Profile - either a built-in emulator profile or a custom one, out of those supported by the chosen emulator
* Platform - the ROM platform/console, out of those that the emulator profile supports
* RomType - See [Rom Types](#rom-types) below

## Paths

For source and destination, only valid Windows file paths are currently supported. The intended use case is for having the source be an SMB file share (either via UNC path or mapped drive), and the destination be a local path. However, any valid file path should work for either. This means that you can get creative with the source if you have a way to mount alternate remote storage at a Windows file path.

Additionally, for destination paths, relativity to the Playnite folder is preserved if you are using a portable installation of Playnite and your destination is below that folder hierarchically. This means that, for example, if your portable installation is at D:\playnite, and you choose `D:\playnite\rominstall` as your destination, it will be saved internally as `{PlayniteDir}\rominstall`.

## Rom Types

### SingleFile

SingleFile is the simplest type of ROM supported. This is for source folders in which each ROM is fully contained in a single file. It's commonly used for older, non-disc-based systems where the whole ROM consists of a single file. (Ex. .nes, .sfc, .md, etc.). Archive formats are supported as well if the emulator supports them directly. (Ex. .zip)

### MultiFile

With the MultiFile type, each subfolder directly within the source folder is scanned as a potential "ROM". This is for games that have multiple loose files. (Ex. one or more .bin/.cue, with optional .m3u). When installing a MultiFile game, the whole folder is copied. 

To determine which file is used as the one to tell the emulator to load, all files matching the configured emulator profile's supported extensions are considered. Precedence is by configured image extension list order, and then by alphabetical order. For example, if file names are the same except for `(Disc 1)` versus `(Disc 2)`, the first disc takes precedence. Similarly, if you have `.cue` in the extension list before `.m3u` (as some of the built-in profiles have at the time of writing), `.cue` would be chosen over `.m3u`, which may not be desired for multi-disc games.

### M3uPlaylist

M3uPlaylist is a special type of MultiFile ROM that uses M3U files to determine the files to copy for multi-file games in subfolders. When a M3U file is present, it will be used to identify the main game files, allowing for more complex folder structures and file arrangements.

## Support

Since this is a personal fork, I cannot guarantee support for it. However, I will try to help if you have questions or issues. You can open an issue on the GitHub repository, and I will do my best to respond in a timely manner.
