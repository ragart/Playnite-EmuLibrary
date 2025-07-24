using Playnite.SDK.Models;

namespace EmuLibrary.RomTypes
{
    internal static class ELGameInfoBaseExtensions
    {
        public static ELGameInfo GetELGameInfo(this Game game)
        {
            return ELGameInfo.FromGame<ELGameInfo>(game);
        }

        public static ELGameInfo GetELGameInfo(this GameMetadata game)
        {
            return ELGameInfo.FromGameMetadata<ELGameInfo>(game);
        }

        public static M3uPlaylist.M3uPlaylistGameInfo GetM3uPlaylistGameInfo(this Game game)
        {
            return ELGameInfo.FromGame<M3uPlaylist.M3uPlaylistGameInfo>(game);
        }
    }
}