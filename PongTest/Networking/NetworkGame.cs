#region Using

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Emotion.Common;
using PongTest.NetGame;

#endregion

namespace PongTest.Networking
{
    public enum GameState
    {
        Waiting,
        Running,
        Dead
    }

    public class NetworkGame
    {
        public GameState State = GameState.Waiting;
        public Server Host;
        public string Id { get; }
        public List<NetworkPlayer> Players = new List<NetworkPlayer>();

        public NetworkGame()
        {
            Id = Network.GenerateId("Game");
            Task.Run(RunGameNetwork);
        }

        public async Task SendToEveryone(NetworkMessage msg)
        {
            Task[] tasks;
            lock (Players)
            {
                tasks = new Task[Players.Count];
                for (var i = 0; i < Players.Count; i++)
                {
                    NetworkPlayer p = Players[i];
                    if (p.Socket == null) continue;
                    tasks[i] = Network.GenericSendMessage(p.Socket, msg);
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task<NetworkMessage[]> SendToEveryoneAndAwaitReplies(NetworkMessage msg)
        {
            Task<NetworkMessage>[] tasks;
            lock (Players)
            {
                tasks = new Task<NetworkMessage>[Players.Count];
                for (var i = 0; i < Players.Count; i++)
                {
                    tasks[i] = Players[i].SendMessage(msg);
                }
            }

            return await Task.WhenAll(tasks);
        }

        public virtual async Task<bool> AddPlayer(NetworkPlayer player)
        {
            if (State != GameState.Waiting)
            {
                Engine.Log.Warning("Can't join a game in progress.", $"Game-{Id}");
                return false;
            }

            Engine.Log.Warning($"Joined game {Id}.", $"{player.Id}");
            lock (Players)
            {
                Players.Add(player);
            }

            return true;
        }

        protected async Task StartGame()
        {
            State = GameState.Running;
            var gameStart = new NetworkMessage
            {
                MessageType = MessageType.GameStart
            };
            await SendToEveryone(gameStart);
        }

        public async Task StopGame()
        {
            State = GameState.Dead;
            var gameOver = new NetworkMessage
            {
                MessageType = MessageType.GameStop
            };
            await SendToEveryone(gameOver);
        }

        protected virtual NetworkScene CreateNetworkScene()
        {
            return null;
        }

        protected async Task RunGameNetwork()
        {
            // Wait for game to start.
            while (State == GameState.Waiting) await Task.Delay(100);
            if (State == GameState.Dead) return;

            // Load a game instance.
            NetSceneDeltaState deltaState = new();
            NetworkScene scene = CreateNetworkScene();
            if (scene == null)
            {
                await StopGame();
                return;
            }

            scene.LoadServer(deltaState);

            // Send initial game sync.
            {
                await using var stream = new MemoryStream();
                scene.WriteSceneToStream(stream, false, deltaState);
                var initialStateSync = new NetworkMessage(stream.GetBuffer())
                {
                    MessageType = MessageType.GameUpdate
                };
                await SendToEveryoneAndAwaitReplies(initialStateSync);
                await SendToEveryone(new NetworkMessage
                {
                    MessageType = MessageType.GameLoopStart
                });
                scene.PostLoadServer(deltaState);
            }

            // Start the game loop.
            int tickRate = scene.TicksPerSecond;
            int msBetweenTicks = 1000 / tickRate;
            float sceneUpdateTimes = msBetweenTicks / Engine.DeltaTime;
            float gameTime = 0;
            var tickSleep = Stopwatch.StartNew();
            while (State == GameState.Running)
            {
                Task<NetworkMessage[]> tickRequest = SendToEveryoneAndAwaitReplies(new NetworkMessage
                {
                    MessageType = MessageType.RequestTick
                });

                while (tickSleep.ElapsedMilliseconds < msBetweenTicks) { }
                tickSleep.Restart();

                NetworkMessage[] tickResponses = await tickRequest;
                for (var i = 0; i < tickResponses.Length; i++)
                {
                    NetworkMessage playerMsg = tickResponses[i];
                    await using var stream = new MemoryStream(playerMsg.Data);
                    scene.ReadSceneFromStream(stream);
                }

                scene.PreServerUpdate(deltaState);
                for (var i = 0; i < sceneUpdateTimes; i++)
                {
                    scene.UpdateServer(Engine.DeltaTime, deltaState);
                }

                gameTime += msBetweenTicks;
                await using var str = new MemoryStream();
                scene.WriteSceneToStream(str, false, deltaState, gameTime);
                await SendToEveryone(new NetworkMessage(str.GetBuffer())
                {
                    MessageType = MessageType.GameUpdate
                });
                //Engine.Log.Trace("Server tick done", "NetworkGame");
            }
        }
    }
}