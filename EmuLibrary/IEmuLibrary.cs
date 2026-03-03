using EmuLibrary.RomTypes;
using Playnite.SDK;
using System.Threading;

namespace EmuLibrary
{
    internal interface IEmuLibrary
    {
        ILogger Logger { get; }
        IPlayniteAPI Playnite { get; }
        Settings.Settings Settings { get; }
        string GetPluginUserDataPath();
        RomTypeScanner GetScanner(RomType romType);
        void ConvertInstalledGamesToCurrentInstallMethod(bool promptUser, CancellationToken ct);
        void DryRunConvertInstalledGamesToCurrentInstallMethod(CancellationToken ct);
    }
}
