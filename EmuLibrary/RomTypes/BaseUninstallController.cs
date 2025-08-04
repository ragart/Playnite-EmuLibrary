
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.IO;

namespace EmuLibrary.RomTypes
{
    internal abstract class BaseUninstallController : UninstallController
    {
        protected readonly IEmuLibrary _emuLibrary;

        protected bool ShowFullPaths => _emuLibrary.Settings.ShowFullPaths;

        internal BaseUninstallController(Game game, IEmuLibrary emuLibrary) : base(game)
        {
            Name = "Uninstall";
            _emuLibrary = emuLibrary;
        }

        protected abstract string GetSourcePath();

        protected void OnUninstalled()
        {
            if (_emuLibrary.Settings.AutoRemoveUninstalledGamesMissingFromSource)
            {
                var sourcePath = GetSourcePath();
                if (!string.IsNullOrEmpty(sourcePath) && !File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                {
                    _emuLibrary.Playnite.Database.Games.Remove(Game);
                    return;
                }
            }

            InvokeOnUninstalled(new GameUninstalledEventArgs());
        }
    }
}
