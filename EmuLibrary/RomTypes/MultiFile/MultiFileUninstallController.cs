using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.IO;
using System.Windows;

namespace EmuLibrary.RomTypes.MultiFile
{
    internal class MultiFileUninstallController : BaseUninstallController
    {
        internal MultiFileUninstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        { }

        public override void Uninstall(UninstallActionArgs args)
        {
            var info = Game.GetMultiFileGameInfo();
            var gameInstallDirectoryResolved = info.DestinationFullBaseDir.Replace(ExpandableVariables.PlayniteDirectory, _emuLibrary.Playnite.Paths.ApplicationPath);

            if (new DirectoryInfo(gameInstallDirectoryResolved).Exists)
            {
                Directory.Delete(gameInstallDirectoryResolved, true);
            }
            else
            {
                _emuLibrary.Playnite.Dialogs.ShowMessage($"\"{Game.Name}\" does not appear to be installed. Marking as uninstalled.", "Game not installed", MessageBoxButton.OK);
            }

            // Update the rom path to point back to the source
            var srcPath = ShowFullPaths ? info.SourceFullPath : info.SourcePath;
            if (_emuLibrary.Playnite.ApplicationInfo.IsPortable)
            {
                srcPath = srcPath.Replace(_emuLibrary.Playnite.Paths.ApplicationPath, ExpandableVariables.PlayniteDirectory);
            }
            Game.Roms = new System.Collections.ObjectModel.ObservableCollection<GameRom> { new GameRom(Game.Name, srcPath) };

            InvokeOnUninstalled(new GameUninstalledEventArgs());
        }
    }
}
