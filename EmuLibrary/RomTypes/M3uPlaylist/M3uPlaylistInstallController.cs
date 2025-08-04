using EmuLibrary.Util.FileCopier;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.RomTypes.M3uPlaylist
{
    internal class M3uPlaylistInstallController : BaseInstallController
    {
        internal M3uPlaylistInstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        { }

        public override void Install(InstallActionArgs args)
        {
            if (!ValidateInstallRequirements())
                return;
            var info = Game.GetM3uPlaylistGameInfo();
            var srcPath = info.SourceFullPath;
            var dstPath = info.DestinationFullPath;

            _watcherToken = new CancellationTokenSource();

            Task.Run(async () =>
            {
                try
                {
                    var source = new FileInfo(srcPath);
                    var destination = new FileInfo(dstPath);

                    var referencedDirectories = File.ReadAllLines(source.FullName)
                        .Select(line => Path.GetDirectoryName(line))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Select(dir => new DirectoryInfo(Path.Combine(source.DirectoryName ?? string.Empty, dir)))
                        .ToList();

                    var fileCopiers = new List<IFileCopier>();

                    foreach (var dir in referencedDirectories)
                    {
                        var destinationDir = new DirectoryInfo(Path.Combine(destination.DirectoryName ?? string.Empty, dir.Name));
                        fileCopiers.Add(CreateFileCopier(dir, destinationDir));
                    }

                    fileCopiers.Add(CreateFileCopier(source, destination.Directory));

                    foreach (var copier in fileCopiers)
                    {
                        await copier.CopyAsync(_watcherToken.Token);
                    }

                    var installDir = info.DestinationFullBaseDir;
                    var gamePath = ShowFullPaths ? info.DestinationFullPath : info.DestinationPath;

                    if (_emuLibrary.Playnite.ApplicationInfo.IsPortable)
                    {
                        installDir = installDir.Replace(_emuLibrary.Playnite.Paths.ApplicationPath, ExpandableVariables.PlayniteDirectory);
                        gamePath = gamePath.Replace(_emuLibrary.Playnite.Paths.ApplicationPath, ExpandableVariables.PlayniteDirectory);
                    }

                    var installationData = new GameInstallationData
                    {
                        InstallDirectory = installDir,
                        Roms = new List<GameRom> { new GameRom(Game.Name, gamePath) }
                    };

                    InvokeOnInstalled(new GameInstalledEventArgs(installationData));
                }
                catch (Exception ex)
                {
                    if (!(ex is WindowsCopyDialogClosedException))
                    {
                        _emuLibrary.Playnite.Notifications.Add(Game.GameId, $"Failed to install {Game.Name}.{Environment.NewLine}{Environment.NewLine}{ex}", NotificationType.Error);
                    }
                    Game.IsInstalling = false;
                    throw;
                }
            });
        }
    }
}