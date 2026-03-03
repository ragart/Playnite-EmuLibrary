using Microsoft.VisualBasic.FileIO;
using Playnite.SDK;
using System;
using System.IO;

namespace EmuLibrary.Util.FileCopier
{
    public class WindowsFileCopier : BaseFileCopier, IFileCopier
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        public WindowsFileCopier(FileSystemInfo source, DirectoryInfo destination) : base(source, destination) { }

        protected override void Copy()
        {
            try
            {
                if (Source is DirectoryInfo)
                {
                    FileSystem.CopyDirectory(Source.FullName, Destination.FullName, UIOption.AllDialogs);
                    return;
                }
                FileSystem.CopyFile(Source.FullName, Path.Combine(Destination.FullName, Source.Name), UIOption.AllDialogs);
            }
            catch (Exception ex)
            {
                try
                {
                    // For directories, some child nodes may have been partially copied before cancellation. Clean these up.
                    if (Source is DirectoryInfo)
                    {
                        FileSystem.DeleteDirectory(Destination.FullName, UIOption.OnlyErrorDialogs, RecycleOption.DeletePermanently);
                    }
                    // Remove the file if for some reason it still exists after user cancellation.
                    else if (Source is FileInfo)
                    {
                        FileSystem.DeleteFile(Destination.FullName, UIOption.OnlyErrorDialogs, RecycleOption.DeletePermanently);
                    }
                }
                catch (Exception cleanupEx)
                {
                    Logger.Warn($"Cleanup after failed/canceled copy could not fully complete for destination '{Destination.FullName}'. {cleanupEx}");
                }
                if (ex is OperationCanceledException)
                {
                    throw new WindowsCopyDialogClosedException("The user cancelled the copy request", ex);
                }
                throw new FileCopyOperationException($"Unable to copy source \"{Source.FullName}\" to destination \"{Destination.FullName}\"", ex);
            }
        }
    }

    public class WindowsCopyDialogClosedException : Exception
    {
        public WindowsCopyDialogClosedException(string message, Exception ex) : base(message, ex) { }
    }
}
