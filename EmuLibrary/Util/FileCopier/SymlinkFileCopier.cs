using System;
using System.Diagnostics;
using System.IO;

namespace EmuLibrary.Util.FileCopier
{
    public class SymlinkFileCopier : BaseFileCopier, IFileCopier
    {
        private readonly bool _fallbackToHardlink;

        public SymlinkFileCopier(FileSystemInfo source, DirectoryInfo destination, bool fallbackToHardlink = true) : base(source, destination)
        {
            _fallbackToHardlink = fallbackToHardlink;
        }

        protected override void Copy()
        {
            Directory.CreateDirectory(Destination.FullName);

            if (Source is DirectoryInfo)
            {
                CreateDirectorySymlinks(Source as DirectoryInfo, Destination);
                return;
            }

            CreateFileSymlink(Source as FileInfo, Destination);
        }

        private void CreateDirectorySymlinks(DirectoryInfo source, DirectoryInfo destination)
        {
            foreach (var file in source.GetFiles())
            {
                CreateFileSymlink(file, destination);
            }

            foreach (var subDirectory in source.GetDirectories())
            {
                var subDestination = destination.CreateSubdirectory(subDirectory.Name);
                CreateDirectorySymlinks(subDirectory, subDestination);
            }
        }

        private void CreateFileSymlink(FileInfo sourceFile, DirectoryInfo destination)
        {
            var linkPath = Path.Combine(destination.FullName, sourceFile.Name);

            if (File.Exists(linkPath))
            {
                File.Delete(linkPath);
            }

            CreateSymlinkWithOptionalFallback(linkPath, sourceFile.FullName);
        }

        private void CreateSymlinkWithOptionalFallback(string linkPath, string targetPath)
        {
            if (TryCreateMklink($"/c mklink \"{linkPath}\" \"{targetPath}\"", out var symlinkError))
            {
                return;
            }

            if (_fallbackToHardlink && AreOnSameVolume(linkPath, targetPath) && TryCreateMklink($"/c mklink /H \"{linkPath}\" \"{targetPath}\"", out _))
            {
                return;
            }

            if (_fallbackToHardlink)
            {
                throw new SymlinkCreationException($"Failed to create symlink from \"{linkPath}\" to \"{targetPath}\". Symlink error: {symlinkError}. Hardlink fallback also failed.");
            }

            throw new SymlinkCreationException($"Failed to create symlink from \"{linkPath}\" to \"{targetPath}\". {symlinkError}");
        }

        private static bool TryCreateMklink(string arguments, out string error)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            });

            if (process == null)
            {
                error = "Unable to start cmd.exe for mklink.";
                return false;
            }

            process.WaitForExit();

            var stderr = process.StandardError.ReadToEnd();
            var stdout = process.StandardOutput.ReadToEnd();

            if (process.ExitCode == 0)
            {
                error = string.Empty;
                return true;
            }

            error = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            return false;
        }

        private static bool AreOnSameVolume(string path1, string path2)
        {
            var root1 = Path.GetPathRoot(path1);
            var root2 = Path.GetPathRoot(path2);
            return !string.IsNullOrEmpty(root1)
                   && !string.IsNullOrEmpty(root2)
                   && string.Equals(root1, root2, StringComparison.OrdinalIgnoreCase);
        }
    }
}