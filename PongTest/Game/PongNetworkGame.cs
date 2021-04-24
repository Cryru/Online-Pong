#region Using

using System.Threading.Tasks;
using PongTest.NetGame;
using PongTest.Networking;

#endregion

namespace PongTest.Game
{
    public class PongNetworkGame : NetworkGame
    {
        public override async Task<bool> AddPlayer(NetworkPlayer player)
        {
            int playerCount;
            lock (Players)
            {
                playerCount = Players.Count;
                if (playerCount == 2) return false;
            }

            bool added = await base.AddPlayer(player);
            if (!added) return false;

            // Check if we should start game.
            playerCount++;
            if (playerCount == 2) await StartGame();
            return true;
        }

        protected override NetworkScene CreateNetworkScene()
        {
            return new PongGameScene(this);
        }
    }
}