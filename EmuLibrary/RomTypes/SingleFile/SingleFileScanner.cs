using EmuLibrary.PlayniteCommon;
using EmuLibrary.Settings;
using EmuLibrary.Util;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace EmuLibrary.RomTypes.SingleFile
{
    internal class SingleFileScanner : RomTypeScanner
    {
        private readonly IPlayniteAPI _playniteAPI;

        // Hack to exclude anything past disc one for games we're not treating as multi-file / m3u but have multiple discs :|
        static private readonly Regex s_discXpattern = new Regex(@"\((?:Disc|Disk) \d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public override RomType RomType => RomType.SingleFile;

        public SingleFileScanner(IEmuLibrary emuLibrary) : base(emuLibrary)
        {
            _playniteAPI = emuLibrary.Playnite;
        }

        public override IEnumerable<GameMetadata> GetGames(EmulatorMapping mapping, LibraryGetGamesArgs args)
        {
            if (args.CancelToken.IsCancellationRequested)
                yield break;

            var imageExtensionsLower = mapping.ImageExtensionsLower;
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

                    foreach (var extension in imageExtensionsLower)
                    {
                        if (args.CancelToken.IsCancellationRequested)
                            yield break;

                        if (HasMatchingExtension(file, extension) && !s_discXpattern.IsMatch(file.Name))
                        {
                            var baseFileName = StringExtensions.GetPathWithoutAllExtensions(Path.GetFileName(file.Name));
                            var gameName = StringExtensions.NormalizeGameName(baseFileName);
                            var info = new SingleFileGameInfo()
                            {
                                MappingId = mapping.MappingId,
                                SourcePath = file.Name,
                            };

                            yield return new GameMetadata()
                            {
                                Source = EmuLibrary.SourceName,
                                Name = gameName,
                                Roms = new List<GameRom>() { new GameRom(gameName, _playniteAPI.Paths.IsPortable ? file.FullName.Replace(_playniteAPI.Paths.ApplicationPath, Playnite.SDK.ExpandableVariables.PlayniteDirectory) : file.FullName) },
                                InstallDirectory = _playniteAPI.Paths.IsPortable ? dstPath.Replace(_playniteAPI.Paths.ApplicationPath, Playnite.SDK.ExpandableVariables.PlayniteDirectory) : dstPath,
                                IsInstalled = true,
                                GameId = info.AsGameId(),
                                Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                                Regions = FileNameUtils.GuessRegionsFromRomName(baseFileName).Select(r => new MetadataNameProperty(r)).ToHashSet<MetadataProperty>(),
                                InstallSize = (ulong)new FileInfo(file.FullName).Length,
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
                }
            }
            #endregion

            #region Import "uninstalled" games
            if (Directory.Exists(srcPath))
            {
                fileEnumerator = new SafeFileEnumerator(srcPath, "*.*", SearchOption.TopDirectoryOnly);

                foreach (var file in fileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                        yield break;

                    foreach (var extension in imageExtensionsLower)
                    {
                        if (args.CancelToken.IsCancellationRequested)
                            yield break;

                        if (HasMatchingExtension(file, extension) && !s_discXpattern.IsMatch(file.Name))
                        {
                            var equivalentInstalledPath = Path.Combine(dstPath, file.Name);
                            if (File.Exists(equivalentInstalledPath))
                            {
                                continue;
                            }

                            var info = new SingleFileGameInfo()
                            {
                                MappingId = mapping.MappingId,
                                SourcePath = file.Name,
                            };

                            var baseFileName = StringExtensions.GetPathWithoutAllExtensions(Path.GetFileName(file.Name));
                            var gameName = StringExtensions.NormalizeGameName(baseFileName);

                            yield return new GameMetadata()
                            {
                                Source = EmuLibrary.SourceName,
                                Name = gameName,
                                Roms = new List<GameRom>() { new GameRom(gameName, _playniteAPI.Paths.IsPortable ? file.FullName.Replace(_playniteAPI.Paths.ApplicationPath, Playnite.SDK.ExpandableVariables.PlayniteDirectory) : file.FullName) },
                                IsInstalled = false,
                                GameId = info.AsGameId(),
                                Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                                Regions = FileNameUtils.GuessRegionsFromRomName(baseFileName).Select(r => new MetadataNameProperty(r)).ToHashSet<MetadataProperty>(),
                                InstallSize = (ulong)new FileInfo(file.FullName).Length,
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
                }
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
                if (info.RomType != RomType.SingleFile)
                    return false;

                return !File.Exists((info as SingleFileGameInfo).SourceFullPath);
            });
        }
    }
}
