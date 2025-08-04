using EmuLibrary.Util.FileCopier;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.IO;
using System.Threading;

namespace EmuLibrary.RomTypes
{
    internal abstract class BaseInstallController : InstallController
    {
        protected readonly IEmuLibrary _emuLibrary;
        protected CancellationTokenSource _watcherToken;

        internal BaseInstallController(Game game, IEmuLibrary emuLibrary) : base(game)
        {
            Name = "Install";
            _emuLibrary = emuLibrary;
        }

        public virtual bool ValidateInstallRequirements()
        {
            var info = Game.GetELGameInfo();
            if (info == null)
            {
                _emuLibrary.Playnite.Dialogs.ShowErrorMessage($"Game information is missing for \"{Game.Name}\".", "Installation Error");
                return false;
            }
            if (!info.CheckSourceExists())
            {
                info.HandleMissingSource(Game, _emuLibrary);
                _emuLibrary.Playnite.Dialogs.ShowErrorMessage($"Game source for \"{Game.Name}\" is missing. Cannot proceed with installation.", "Installation Error");
                return false;
            }
            return true;
        }

        public override void Dispose()
        {
            _watcherToken?.Cancel();
            base.Dispose();
        }

        protected bool ShowFullPaths => _emuLibrary.Settings.ShowFullPaths;

        private bool UseWindowsCopyDialog()
        {
            if (_emuLibrary.Playnite.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                return _emuLibrary.Settings.UseWindowsCopyDialogInDesktopMode;
            }
            else if (_emuLibrary.Playnite.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                return _emuLibrary.Settings.UseWindowsCopyDialogInFullscreenMode;
            }
            return false;
        }

        protected IFileCopier CreateFileCopier(FileSystemInfo source, DirectoryInfo destination)
        {
            if (UseWindowsCopyDialog())
            {
                return new WindowsFileCopier(source, destination);
            }
            return new SimpleFileCopier(source, destination);
        }
    }
}
