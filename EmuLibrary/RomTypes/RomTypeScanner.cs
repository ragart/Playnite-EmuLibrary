using EmuLibrary.Settings;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
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
        
        public abstract GameMetadata GetMetadata(FileSystemInfoBase file, EmulatorMapping mapping, bool isInstalled, string baseDir = null);

        public abstract IEnumerable<Game> GetGamesMissingSourceFiles(CancellationToken ct, bool isInstalled = false);
        
        protected static bool HasMatchingExtension(FileSystemInfoBase file, string extension)
        {
            var fileExtension = file.Extension?.TrimStart('.') ?? string.Empty;
            return string.Equals(fileExtension, extension, StringComparison.OrdinalIgnoreCase)
                   || (fileExtension.Length == 0 && string.Equals(extension, "<none>", StringComparison.OrdinalIgnoreCase));
        }
    }
}
