using EmuLibrary.PlayniteCommon;
using EmuLibrary.Settings;
using EmuLibrary.Util;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace EmuLibrary.RomTypes.M3uPlaylist
{
    internal class M3uPlaylistScanner : RomTypeScanner
    {
        private readonly IPlayniteAPI _playniteAPI;

        public override RomType RomType => RomType.M3uPlaylist;

        public M3uPlaylistScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
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
            SafeFileEnumerator fileEnumerator;

            #region Import "installed" games

            if (Directory.Exists(dstPath))
            {
                fileEnumerator = new SafeFileEnumerator(dstPath, "*.*", SearchOption.TopDirectoryOnly);

                foreach (var file in fileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                        yield break;

                    if (!extensionsLower.Any(ext => HasMatchingExtension(file, ext)))
                        continue;

                    var baseFileName = StringExtensions.GetPathWithoutAllExtensions(Path.GetFileName(file.Name));
                    var gameName = baseFileName.NormalizeGameName();

                    var info = new M3uPlaylistGameInfo()
                    {
                        MappingId = mapping.MappingId,
                        SourcePath = file.Name,
                        SourceBaseDir = "",
                        DestinationPath = file.Name,
                        DestinationBaseDir = "",
                    };

                    var romPath = _playniteAPI.Paths.IsPortable
                        ? file.FullName.Replace(
                            _playniteAPI.Paths.ApplicationPath,
                            ExpandableVariables.PlayniteDirectory)
                        : file.FullName;

                    var fileInfo = new FileInfo(file.FullName);

                    yield return new GameMetadata()
                    {
                        Source = EmuLibrary.SourceName,
                        Name = gameName,
                        Roms = new List<GameRom>() { new GameRom(
                            gameName,
                            Settings.Settings.Instance.ShowFullPaths
                                ? romPath
                                : file.Name) },
                        InstallDirectory = _playniteAPI.Paths.IsPortable ? fileInfo.Directory.FullName.Replace(_playniteAPI.Paths.ApplicationPath, ExpandableVariables.PlayniteDirectory) : fileInfo.Directory.FullName,
                        IsInstalled = true,
                        GameId = info.AsGameId(),
                        Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                        Regions = FileNameUtils.GuessRegionsFromRomName(baseFileName).Select(r => new MetadataNameProperty(r)).ToHashSet<MetadataProperty>(),
                        GameActions = new List<GameAction>() { new GameAction()
                        {
                            Name = $"Play in {mapping.Emulator.Name}",
                            Type = GameActionType.Emulator,
                            EmulatorId = mapping.EmulatorId,
                            EmulatorProfileId = mapping.EmulatorProfileId,
                            IsPlayAction = true,
                        } }
                    };
                }
            }
            #endregion

            #region Import "uninstalled" games

            if (!Directory.Exists(srcPath))
                yield break;

            fileEnumerator = new SafeFileEnumerator(srcPath, "*.*", SearchOption.TopDirectoryOnly);

            foreach (var file in fileEnumerator)
            {
                if (args.CancelToken.IsCancellationRequested)
                    yield break;

                if (!extensionsLower.Any(ext => HasMatchingExtension(file, ext)))
                    continue;

                var equivalentInstalledPath = Path.Combine(dstPath, file.Name);

                if (File.Exists(equivalentInstalledPath))
                {
                    continue;
                }

                var info = new M3uPlaylistGameInfo()
                {
                    MappingId = mapping.MappingId,
                    SourcePath = file.Name,
                    SourceBaseDir = "",
                    DestinationPath = file.Name,
                    DestinationBaseDir = ""
                };

                var baseFileName = StringExtensions.GetPathWithoutAllExtensions(Path.GetFileName(file.Name));
                var gameName = baseFileName.NormalizeGameName();

                var romPath = _playniteAPI.Paths.IsPortable
                    ? file.FullName.Replace(
                        _playniteAPI.Paths.ApplicationPath,
                        ExpandableVariables.PlayniteDirectory)
                    : file.FullName;

                yield return new GameMetadata()
                {
                    Source = EmuLibrary.SourceName,
                    Name = gameName,
                    Roms = new List<GameRom>() {new GameRom(
                        gameName,
                        Settings.Settings.Instance.ShowFullPaths
                            ? romPath
                            : file.Name)},
                    IsInstalled = false,
                    GameId = info.AsGameId(),
                    Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                    Regions = FileNameUtils.GuessRegionsFromRomName(baseFileName).Select(r => new MetadataNameProperty(r)).ToHashSet<MetadataProperty>(),
                    GameActions = new List<GameAction>() { new GameAction()
                    {
                        Name = $"Play in {mapping.Emulator.Name}",
                        Type = GameActionType.Emulator,
                        EmulatorId = mapping.EmulatorId,
                        EmulatorProfileId = mapping.EmulatorProfileId,
                        IsPlayAction = true,
                    } }
                };
            }
            #endregion
        }

        public override IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct)
        {
            return _playniteAPI.Database.Games.TakeWhile(g => !ct.IsCancellationRequested)
                .Where(g =>
            {
                if (g.PluginId != EmuLibrary.PluginId || g.IsInstalled)
                    return false;

                var info = g.GetELGameInfo();
                if (info.RomType != RomType.M3uPlaylist)
                    return false;

                return !File.Exists((info as M3uPlaylistGameInfo)?.SourceFullPath);
            });
        }
    }
}