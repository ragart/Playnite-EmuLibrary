using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.IO;
using System.Windows;

namespace EmuLibrary.RomTypes.MultiFile
{
    class MultiFileUninstallController : UninstallController
    {
        private readonly IEmuLibrary _emuLibrary;

        internal MultiFileUninstallController(Game game, IEmuLibrary emuLibrary) : base(game)
        {
            Name = "Uninstall";
            _emuLibrary = emuLibrary;
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            var info = Game.GetMultiFileGameInfo();
            var srcPath = info.SourceFullFilePath;

            var gameInstallDirectoryResolved = Game.InstallDirectory.Replace(ExpandableVariables.PlayniteDirectory, _emuLibrary.Playnite.Paths.ApplicationPath);
            if (new DirectoryInfo(gameInstallDirectoryResolved).Exists)
            {
                Directory.Delete(gameInstallDirectoryResolved, true);
            }
            else
            {
                _emuLibrary.Playnite.Dialogs.ShowMessage($"\"{Game.Name}\" does not appear to be installed. Marking as uninstalled.", "Game not installed", MessageBoxButton.OK);
            }
            Game.Roms = new System.Collections.ObjectModel.ObservableCollection<GameRom>(new GameRom[] { new GameRom(Game.Name, srcPath) });
            InvokeOnUninstalled(new GameUninstalledEventArgs());
        }
    }
}
