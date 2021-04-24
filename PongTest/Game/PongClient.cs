#region Using

using System.IO;
using System.Threading.Tasks;
using Emotion.Common;
using PongTest.Networking;

#endregion

namespace PongTest.Game
{
    public class PongClient : Client
    {
        public PongClient(string defaultIp, int defaultPort) : base(defaultIp, defaultPort)
        {
        }

        protected override async Task OnConnectedToServer()
        {
            var getGameList = new NetworkMessage
            {
                MessageType = MessageType.GameList
            };
            NetworkMessage gameList = await Server.SendMessage(getGameList);
            await using var str = new MemoryStream(gameList.Data);
            using var reader = new BinaryReader(str);
            int gameCount = reader.ReadInt32();
            var gameIds = new string[gameCount];
            for (var i = 0; i < gameCount; i++)
            {
                gameIds[i] = reader.ReadString();
                int players = reader.ReadInt32();
                for (var j = 0; j < players; j++)
                {
                    // dont store for now
                    reader.ReadString();
                }
            }

            // Join first game or create a new one.
            NetworkMessage joinedMessage;
            if (gameCount == 0)
            {
                var gameCreate = new NetworkMessage
                {
                    MessageType = MessageType.GameJoin
                };
                joinedMessage = await Server.SendMessage(gameCreate);
            }
            else
            {
                var gameJoin = new NetworkMessage(gameIds[0])
                {
                    MessageType = MessageType.GameJoin
                };
                joinedMessage = await Server.SendMessage(gameJoin);
            }

            bool joinSuccess = joinedMessage?.Data != null && joinedMessage.Data[0] == 1;
            if (!joinSuccess)
            {
                Engine.Log.Warning("Couldn't join game.", $"{Handle}");
                return;
            }

            var gameScene = new PongGameScene(Handle);
            await RunGameLoop(gameScene);
        }
    }
}