
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;

namespace EmuLibrary.RomTypes.M3uPlaylist
{
    [ProtoContract]
    internal class M3uPlaylistGameInfo : ELGameInfo
    {
        public override RomType RomType => RomType.M3uPlaylist;

        public override InstallController GetInstallController(Game game, IEmuLibrary emuLibrary) =>
            new M3uPlaylistInstallController(game, emuLibrary);

        public override UninstallController GetUninstallController(Game game, IEmuLibrary emuLibrary) =>
            new M3uPlaylistUninstallController(game, emuLibrary);

        public override bool CheckSourceExists()
        {
            return File.Exists(SourceFullPath);
        }

        protected override IEnumerable<string> GetDescriptionLines()
        {
            yield return $"{nameof(SourcePath)}: {SourcePath}";
            yield return $"{nameof(SourceBaseDir)}: {SourceBaseDir}";
            yield return $"{nameof(SourceFullBaseDir)}*: {SourceFullBaseDir}";
            yield return $"{nameof(DestinationPath)}: {DestinationPath}";
            yield return $"{nameof(DestinationBaseDir)}: {DestinationBaseDir}";
            yield return $"{nameof(DestinationFullBaseDir)}*: {DestinationFullBaseDir}";
        }

        public override void BrowseToSource()
        {
            System.Diagnostics.Process.Start("explorer.exe", $"{Path.GetFullPath(SourceFullPath)}");
        }
    }
}
