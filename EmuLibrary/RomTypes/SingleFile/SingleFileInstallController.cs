using EmuLibrary.Util.FileCopier;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace EmuLibrary.RomTypes.SingleFile
{
    internal class SingleFileInstallController : BaseInstallController
    {
        internal SingleFileInstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        { }

        public override void Install(InstallActionArgs args)
        {
            var info = Game.GetSingleFileGameInfo();
            var srcPath = info.SourceFullPath;
            var dstPath = info.Mapping.DestinationPathResolved;

            _watcherToken = new CancellationTokenSource();

            Task.Run(async () =>
            {
                try
                {
                    var source = new FileInfo(srcPath);
                    var destination = new DirectoryInfo(dstPath);

                    await CreateFileCopier(source, destination).CopyAsync(_watcherToken.Token);

                    var installDir = dstPath;
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
