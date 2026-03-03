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

        protected static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return "<none>";

            var normalized = extension.Trim();
            if (normalized.StartsWith("."))
                normalized = normalized.Substring(1);

            return normalized.Length == 0 ? "<none>" : normalized.ToLowerInvariant();
        }
        
        protected static bool HasMatchingExtension(FileSystemInfoBase file, string extension)
        {
            var normalizedFileExtension = NormalizeExtension(file.Extension);
            return string.Equals(normalizedFileExtension, NormalizeExtension(extension), StringComparison.OrdinalIgnoreCase);
        }

        protected static bool HasMatchingExtension(FileSystemInfoBase file, ISet<string> normalizedExtensions)
        {
            if (normalizedExtensions == null || normalizedExtensions.Count == 0)
                return false;

            return normalizedExtensions.Contains(NormalizeExtension(file.Extension));
        }
    }
}
