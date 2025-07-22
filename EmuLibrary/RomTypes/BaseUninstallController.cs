
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

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
    }
}
