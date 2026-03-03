using System;
using System.Diagnostics;
using System.IO;

namespace EmuLibrary.Util.FileCopier
{
    public class HardlinkFileCopier : BaseFileCopier, IFileCopier
    {
        public HardlinkFileCopier(FileSystemInfo source, DirectoryInfo destination) : base(source, destination) { }

        protected override void Copy()
        {
            Directory.CreateDirectory(Destination.FullName);

            if (Source is DirectoryInfo)
            {
                CreateDirectoryHardlinks(Source as DirectoryInfo, Destination);
                return;
            }

            CreateFileHardlink(Source as FileInfo, Destination);
        }

        private static void CreateDirectoryHardlinks(DirectoryInfo source, DirectoryInfo destination)
        {
            foreach (var file in source.GetFiles())
            {
                CreateFileHardlink(file, destination);
            }

            foreach (var subDirectory in source.GetDirectories())
            {
                var subDestination = destination.CreateSubdirectory(subDirectory.Name);
                CreateDirectoryHardlinks(subDirectory, subDestination);
            }
        }

        private static void CreateFileHardlink(FileInfo sourceFile, DirectoryInfo destination)
        {
            var linkPath = Path.Combine(destination.FullName, sourceFile.Name);

            if (File.Exists(linkPath))
            {
                File.Delete(linkPath);
            }

            if (!AreOnSameVolume(linkPath, sourceFile.FullName))
            {
                throw new HardlinkCreationException($"Failed to create hardlink from \"{linkPath}\" to \"{sourceFile.FullName}\". Hardlinks require source and destination on the same volume.");
            }

            if (!TryCreateMklink($"/c mklink /H \"{linkPath}\" \"{sourceFile.FullName}\"", out var error))
            {
                throw new HardlinkCreationException($"Failed to create hardlink from \"{linkPath}\" to \"{sourceFile.FullName}\". {error}");
            }
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