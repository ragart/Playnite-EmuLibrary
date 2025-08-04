using EmuLibrary.Util.FileCopier;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.RomTypes.MultiFile
{
    internal class MultiFileInstallController : BaseInstallController
    {
        internal MultiFileInstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        { }

        public override void Install(InstallActionArgs args)
        {
            if (!ValidateInstallRequirements())
                return;
            var info = Game.GetMultiFileGameInfo();
            var srcPath = info.SourceFullBaseDir;
            var dstPath = info.DestinationFullBaseDir;

            _watcherToken = new CancellationTokenSource();

            Task.Run(async () =>
            {
                try
                {
                    var source = new DirectoryInfo(srcPath);
                    var destination = new DirectoryInfo(dstPath);

                    await CreateFileCopier(source, destination).CopyAsync(_watcherToken.Token);

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
