using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;

namespace EmuLibrary.RomTypes.MultiFile
{
    [ProtoContract]
    internal class MultiFileGameInfo : ELGameInfo
    {
        public override RomType RomType => RomType.MultiFile;

        public override InstallController GetInstallController(Game game, IEmuLibrary emuLibrary) =>
            new MultiFileInstallController(game, emuLibrary);

        public override UninstallController GetUninstallController(Game game, IEmuLibrary emuLibrary) =>
            new MultiFileUninstallController(game, emuLibrary);

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
            System.Diagnostics.Process.Start("explorer.exe", $"\"{Path.GetFullPath(SourceFullBaseDir)}\"");
        }
    }
}
