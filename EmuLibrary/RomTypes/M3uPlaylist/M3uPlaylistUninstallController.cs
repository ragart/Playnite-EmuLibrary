
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.IO;
using System.Linq;

namespace EmuLibrary.RomTypes.M3uPlaylist
{
    internal class M3uPlaylistUninstallController : BaseUninstallController
    {
        public M3uPlaylistUninstallController(Game game, IEmuLibrary emuLibrary) : base(game, emuLibrary)
        { }

        public override void Uninstall(UninstallActionArgs args)
        {
            var info = Game.GetM3uPlaylistGameInfo();
            var installedM3uPath = info.DestinationFullPath;

            if (File.Exists(installedM3uPath))
            {
                var installedM3u = new FileInfo(installedM3uPath);

                var referencedDirectories = File.ReadAllLines(installedM3u.FullName)
                    .Select(line => Path.GetDirectoryName(line))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(dir => new DirectoryInfo(Path.Combine(installedM3u.DirectoryName, dir)))
                    .ToList();

                foreach (var dir in referencedDirectories)
                {
                    if (dir.Exists)
                    {
                        dir.Delete(true);
                    }
                }

                installedM3u.Delete();
            }

            OnUninstalled();
        }
    }
}
