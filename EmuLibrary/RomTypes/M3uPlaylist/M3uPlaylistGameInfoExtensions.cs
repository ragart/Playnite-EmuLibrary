using Playnite.SDK.Models;

namespace EmuLibrary.RomTypes.M3uPlaylist
{
    internal static class M3uPlaylistGameInfoExtensions
    {
        public static M3uPlaylistGameInfo GetM3uPlaylistGameInfo(this Game game)
        {
            return ELGameInfo.FromGame<M3uPlaylistGameInfo>(game);
        }
    }
}
