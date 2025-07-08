﻿using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.IO;
using System.Linq;
using System.Windows;

namespace EmuLibrary.RomTypes.SingleFile
{
    class SingleFileUninstallController : UninstallController
    {
        private readonly IEmuLibrary _emuLibrary;

        internal SingleFileUninstallController(Game game, IEmuLibrary emuLibrary) : base(game)
        {
            Name = "Uninstall";
            _emuLibrary = emuLibrary;
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            var info = Game.GetSingleFileGameInfo();
            var srcPath = info.SourceFullPath;

            var gameImagePathResolved = Game.Roms.First().Path.Replace(ExpandableVariables.PlayniteDirectory, _emuLibrary.Playnite.Paths.ApplicationPath);
            if (new FileInfo(gameImagePathResolved).Exists)
            {
                File.Delete(gameImagePathResolved);
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
