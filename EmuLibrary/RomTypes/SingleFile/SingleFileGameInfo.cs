using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;

namespace EmuLibrary.RomTypes.SingleFile
{
    [ProtoContract]
    internal sealed class SingleFileGameInfo : ELGameInfo
    {
        public override RomType RomType => RomType.SingleFile;

        public override InstallController GetInstallController(Game game, IEmuLibrary emuLibrary) =>
            new SingleFileInstallController(game, emuLibrary);

        public override UninstallController GetUninstallController(Game game, IEmuLibrary emuLibrary) =>
            new SingleFileUninstallController(game, emuLibrary);

        protected override bool CheckSourceExists()
        {
            return File.Exists(SourceFullPath);
        }

        protected override IEnumerable<string> GetDescriptionLines()
        {
            yield return $"{nameof(SourcePath)} : {SourcePath}";
            yield return $"{nameof(SourceFullPath)}* : {SourceFullPath}";
            yield return $"{nameof(DestinationPath)} : {DestinationPath}";
            yield return $"{nameof(DestinationFullPath)}* : {DestinationFullPath}";
        }

        public override void BrowseToSource()
        {
            var psi = new System.Diagnostics.ProcessStartInfo()
            {
                FileName = "explorer.exe",
                Arguments = $"/e, /select, \"{Path.GetFullPath(SourceFullPath)}\""
            };
            System.Diagnostics.Process.Start(psi);
        }
    }
}
