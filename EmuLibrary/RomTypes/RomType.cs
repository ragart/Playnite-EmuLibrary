namespace EmuLibrary.RomTypes
{
    // Don't renumber these, ever. Saved mappings and ELGameInfo field numbers rely on them being static
    public enum RomType
    {
        [RomTypeInfo(typeof(SingleFile.SingleFileGameInfo), typeof(SingleFile.SingleFileScanner))]
        SingleFile = 0,

        [RomTypeInfo(typeof(MultiFile.MultiFileGameInfo), typeof(MultiFile.MultiFileScanner))]
        MultiFile = 1,

        [RomTypeInfo(typeof(M3uPlaylist.M3uPlaylistGameInfo), typeof(M3uPlaylist.M3uPlaylistScanner))]
        M3uPlaylist = 2,
    }
}
