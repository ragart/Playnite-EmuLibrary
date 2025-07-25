using EmuLibrary.PlayniteCommon;
using EmuLibrary.Settings;
using EmuLibrary.Util;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;

namespace EmuLibrary.RomTypes.MultiFile
{
    internal class MultiFileScanner : RomTypeScanner
    {
        private readonly IPlayniteAPI _playniteAPI;

        public override RomType RomType => RomType.MultiFile;

        public MultiFileScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
        {
            _playniteAPI = emuLibrary.Playnite;
        }

        public override IEnumerable<GameMetadata> GetGames(EmulatorMapping mapping, LibraryGetGamesArgs args)
        {
            if (args.CancelToken.IsCancellationRequested)
                yield break;

            var imageExtensionsLower = mapping.ImageExtensionsLower;
            var extensionsLower = imageExtensionsLower as string[] ?? imageExtensionsLower.ToArray();
            var srcPath = mapping.SourcePath;
            var dstPath = mapping.DestinationPathResolved;
            
            var installedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var srcFileEnumerator = new SafeFileEnumerator(srcPath, "*.*", SearchOption.TopDirectoryOnly);
            var dstFileEnumerator = new SafeFileEnumerator(dstPath, "*.*", SearchOption.TopDirectoryOnly);

            if (Directory.Exists(dstPath))
            {
                foreach (var installedFile in dstFileEnumerator)
                {
                    installedFileNames.Add(installedFile.Name);
                }
            }

            // Import installed games
            if (Directory.Exists(dstPath))
            {
                foreach (var file in dstFileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                        yield break;

                    if (!file.Attributes.HasFlag(FileAttributes.Directory))
                        continue;

                    var dirEnumerator = new SafeFileEnumerator(file.FullName, "*.*", SearchOption.AllDirectories);
                    var rom = extensionsLower.Select(ext => dirEnumerator.FirstOrDefault(f => HasMatchingExtension(f, ext))).FirstOrDefault(f => f != null);
                    if (rom == null)
                        continue;

                    yield return GetMetadata(rom, mapping, true);
                }
            }

            // Import uninstalled games
            if (Directory.Exists(srcPath))
            {
                foreach (var file in srcFileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                        yield break;

                    if (!file.Attributes.HasFlag(FileAttributes.Directory))
                        continue;

                    if (installedFileNames.Contains(file.Name))
                        continue;

                    var dirEnumerator = new SafeFileEnumerator(file.FullName, "*.*", SearchOption.AllDirectories);
                    var rom = extensionsLower.Select(ext => dirEnumerator.FirstOrDefault(f => HasMatchingExtension(f, ext))).FirstOrDefault(f => f != null);
                    if (rom == null)
                        continue;

                    yield return GetMetadata(rom, mapping, false);
                }
            }
        }

        public override GameMetadata GetMetadata(FileSystemInfoBase file, EmulatorMapping mapping, bool installed)
        {
            var fileInfo = new FileInfo(file.FullName);
            var baseDirInfo = fileInfo.Directory;

            var info = new MultiFileGameInfo()
            {
                MappingId = mapping.MappingId,
                SourcePath = file.Name,
                SourceBaseDir = baseDirInfo?.Name,
                DestinationPath = file.Name,
                DestinationBaseDir = baseDirInfo?.Name
            };

            var baseFileName = StringExtensions.GetPathWithoutAllExtensions(Path.GetFileName(file.Name));
            var gameName = baseFileName.NormalizeGameName();

            var romPath = _playniteAPI.Paths.IsPortable
                ? file.FullName.Replace(
                    _playniteAPI.Paths.ApplicationPath,
                    ExpandableVariables.PlayniteDirectory)
                : file.FullName;

            var metadata = new GameMetadata()
            {
                Source = EmuLibrary.SourceName,
                Name = gameName,
                Roms = new List<GameRom>() {new GameRom(
                    gameName,
                    Settings.Settings.Instance.ShowFullPaths
                        ? romPath
                        : file.Name)},
                IsInstalled = installed,
                GameId = info.AsGameId(),
                Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                Regions = FileNameUtils.GuessRegionsFromRomName(baseFileName).Select(r => new MetadataNameProperty(r)).ToHashSet<MetadataProperty>(),
                InstallSize = null,
                GameActions = new List<GameAction>() { new GameAction()
                {
                    Name = $"Play in {mapping.Emulator.Name}",
                    Type = GameActionType.Emulator,
                    EmulatorId = mapping.EmulatorId,
                    EmulatorProfileId = mapping.EmulatorProfileId,
                    IsPlayAction = true,
                } }
            };

            if (installed)
                metadata.InstallDirectory = _playniteAPI.Paths.IsPortable
                    ? baseDirInfo?.FullName.Replace(
                        _playniteAPI.Paths.ApplicationPath,
                        ExpandableVariables.PlayniteDirectory)
                    : baseDirInfo?.FullName;

            return metadata;
        }

        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            return _playniteAPI.Database.Games.TakeWhile(g => !ct.IsCancellationRequested)
                .Where(g =>
            {
                if (g.PluginId != EmuLibrary.PluginId || g.IsInstalled)
                    return false;

                var info = g.GetELGameInfo();
                if (info.RomType != RomType.MultiFile)
                    return false;

                return !Directory.Exists((info as MultiFileGameInfo)?.SourceFullBaseDir);
            });
        }
    }
}
