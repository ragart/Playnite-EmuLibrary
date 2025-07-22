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
using System.Text.RegularExpressions;
using System.Threading;

namespace EmuLibrary.RomTypes.MultiFile
{
    internal class MultiFileScanner : RomTypeScanner
    {
        private readonly IPlayniteAPI _playniteAPI;

        // Hack to exclude anything past disc one for games we're not treating as multi-file / m3u but have multiple discs :|
        private static readonly Regex s_discXpattern = new Regex(@"\((?:Disc|Disk) \d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
            SafeFileEnumerator fileEnumerator;

            #region Import "installed" games

            
            if (Directory.Exists(dstPath))
            {
                fileEnumerator = new SafeFileEnumerator(dstPath, "*.*", SearchOption.TopDirectoryOnly);

                foreach (var file in fileEnumerator)
                {
                    if (args.CancelToken.IsCancellationRequested)
                        yield break;

                    if (!file.Attributes.HasFlag(FileAttributes.Directory) || s_discXpattern.IsMatch(file.Name))
                        continue;

                    var dirEnumerator = new SafeFileEnumerator(file.FullName, "*.*", SearchOption.AllDirectories);
                    // First matching rom of first valid extension that has any matches. Ex. for "m3u,cue,bin", make sure we don't grab a bin file when there's a m3u or cue handy
                    var rom = extensionsLower.Select(ext => dirEnumerator.FirstOrDefault(f => HasMatchingExtension(f, ext))).FirstOrDefault(f => f != null);
                    if (rom == null)
                        continue;
                    var baseFileName = StringExtensions.GetPathWithoutAllExtensions(Path.GetFileName(file.Name));
                    var gameName = baseFileName.NormalizeGameName();
                    
                    var relativeRomPath = rom.FullName.Replace(file.FullName, "").TrimStart('\\');
                    var gameBaseDir = baseFileName.NormalizeGameName();
                    
                    var info = new MultiFileGameInfo()
                    {
                        MappingId = mapping.MappingId,
                        SourcePath = Path.Combine(gameBaseDir, relativeRomPath),
                        SourceBaseDir = gameBaseDir,
                        DestinationPath = Path.Combine(gameBaseDir, relativeRomPath),
                        DestinationBaseDir = gameBaseDir, 
                    };

                    var romPath = _playniteAPI.Paths.IsPortable
                        ? rom.FullName.Replace(
                            _playniteAPI.Paths.ApplicationPath,
                            ExpandableVariables.PlayniteDirectory)
                        : rom.FullName;

                    yield return new GameMetadata()
                    {
                        Source = EmuLibrary.SourceName,
                        Name = gameName,
                        Roms = new List<GameRom>() { new GameRom(
                            gameName,
                            Settings.Settings.Instance.ShowFullPaths
                                ? romPath
                                : rom.Name) },
                        InstallDirectory = _playniteAPI.Paths.IsPortable ? file.FullName.Replace(_playniteAPI.Paths.ApplicationPath, ExpandableVariables.PlayniteDirectory) : file.FullName,
                        IsInstalled = true,
                        GameId = info.AsGameId(),
                        Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                        Regions = FileNameUtils.GuessRegionsFromRomName(baseFileName).Select(r => new MetadataNameProperty(r)).ToHashSet<MetadataProperty>(),
                        InstallSize = (ulong)dirEnumerator.Where(f => !f.Attributes.HasFlag(FileAttributes.Directory)).Select(f => new FileInfo(f.FullName)).Sum(f => f.Length),
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

                if (!file.Attributes.HasFlag(FileAttributes.Directory) || s_discXpattern.IsMatch(file.Name))
                    continue;

                var dirEnumerator = new SafeFileEnumerator(file.FullName, "*.*", SearchOption.AllDirectories);
                // First matching rom of first valid extension that has any matches. Ex. for "m3u,cue,bin", make sure we don't grab a bin file when there's a m3u or cue handy
                var rom = extensionsLower.Select(ext => dirEnumerator.FirstOrDefault(f => HasMatchingExtension(f, ext))).FirstOrDefault(f => f != null);
                if (rom == null)
                    continue;
                    
                var fileInfo = new FileInfo(rom.FullName);
                var dirInfo = new DirectoryInfo(file.FullName);
                var equivalentInstalledPath = Path.Combine(dstPath, Path.Combine(new[] { dirInfo.Name, fileInfo.Directory?.FullName.Replace(dirInfo.FullName, "").TrimStart('\\'), fileInfo.Name }));

                if (File.Exists(equivalentInstalledPath))
                {
                    continue;
                }

                var info = new MultiFileGameInfo()
                {
                    MappingId = mapping.MappingId,
                    SourcePath = Path.Combine(file.Name, rom.FullName.Replace(file.FullName, "").TrimStart('\\')),
                    SourceBaseDir = Path.Combine(file.Name),
                    DestinationPath = Path.Combine(file.Name, rom.FullName.Replace(file.FullName, "").TrimStart('\\')),
                    DestinationBaseDir = Path.Combine(file.Name)
                };

                var baseFileName = StringExtensions.GetPathWithoutAllExtensions(Path.GetFileName(file.Name));
                var gameName = baseFileName.NormalizeGameName();

                var romPath = _playniteAPI.Paths.IsPortable
                    ? rom.FullName.Replace(
                        _playniteAPI.Paths.ApplicationPath,
                        ExpandableVariables.PlayniteDirectory)
                    : rom.FullName;

                yield return new GameMetadata()
                {
                    Source = EmuLibrary.SourceName,
                    Name = gameName,
                    Roms = new List<GameRom>() {new GameRom(
                        gameName,
                        Settings.Settings.Instance.ShowFullPaths
                            ? romPath
                            : rom.Name)},
                    IsInstalled = false,
                    GameId = info.AsGameId(),
                    Platforms = new HashSet<MetadataProperty>() { new MetadataNameProperty(mapping.Platform.Name) },
                    Regions = FileNameUtils.GuessRegionsFromRomName(baseFileName).Select(r => new MetadataNameProperty(r)).ToHashSet<MetadataProperty>(),
                    InstallSize = (ulong)dirEnumerator.Where(f => !f.Attributes.HasFlag(FileAttributes.Directory)).Select(f => new FileInfo(f.FullName)).Sum(f => f.Length),
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
                if (info.RomType != RomType.MultiFile)
                    return false;

                return !Directory.Exists((info as MultiFileGameInfo)?.SourceFullBaseDir);
            });
        }
    }
}
