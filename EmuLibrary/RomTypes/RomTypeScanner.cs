using EmuLibrary.Settings;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading;

namespace EmuLibrary.RomTypes
{
    internal abstract class RomTypeScanner
    {
        #pragma warning disable IDE0060 // Remove unused parameter
            protected RomTypeScanner(IEmuLibrary emuLibrary) { }
        #pragma warning restore IDE0060 // Remove unused parameter

        public abstract RomType RomType { get; }

        public abstract IEnumerable<GameMetadata> GetGames(EmulatorMapping mapping, LibraryGetGamesArgs args);
        
        public abstract GameMetadata GetMetadata(FileSystemInfoBase file, EmulatorMapping mapping, bool isInstalled);

        public abstract IEnumerable<Game> GetUninstalledGamesMissingSourceFiles(CancellationToken ct);
        
        protected static bool HasMatchingExtension(FileSystemInfoBase file, string extension)
        {
            return file.Extension.TrimStart('.').ToLower() == extension || (file.Extension == "" && extension == "<none>");
        }
    }
}
