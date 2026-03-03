using System;

namespace EmuLibrary.Util.FileCopier
{
    public class FileCopierValidationException : InvalidOperationException
    {
        public FileCopierValidationException(string message) : base(message) { }
    }

    public class FileCopyOperationException : Exception
    {
        public FileCopyOperationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class HardlinkCreationException : Exception
    {
        public HardlinkCreationException(string message) : base(message) { }
    }

    public class SymlinkCreationException : Exception
    {
        public SymlinkCreationException(string message) : base(message) { }
    }
}