#region Using

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Emotion.Common;

#endregion

namespace PongTest.Networking
{
    public abstract class Server
    {
        public Socket Socket;
        public List<NetworkPlayer> Clients = new();
        public int MaxClients = 10;

        protected Server(int port)
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket.Bind(new IPEndPoint(IPAddress.Any, port));
            Socket.Listen(MaxClients);
            Task.Run(ServerLoop);
        }

        private async void ServerLoop()
        {
            Engine.Log.Trace($"Server started at {Socket.LocalEndPoint}!", "Server");

            try
            {
                while (true)
                {
                    Socket acceptedConnection = await Socket.AcceptAsync();
                    if (Clients.Count >= MaxClients)
                    {
                        acceptedConnection.Close();
                        continue;
                    }

                    Engine.Log.Trace($"Accepted connection from {acceptedConnection.RemoteEndPoint}", "Server");
                    PlayerInit(new NetworkPlayer
                    {
                        Socket = acceptedConnection
                    });
                }
            }
            catch (Exception ex)
            {
                Engine.Log.Error(ex.ToString(), "AA");
            }
        }

        public void PlayerInit(NetworkPlayer player)
        {
            lock (Clients)
            {
                Clients.Add(player);
            }

            Task.Run(() => ClientProcessingLoop(player));
        }

        public void PlayerRemove(NetworkPlayer player)
        {
            lock (Clients)
            {
                Clients.Remove(player);
            }

            player.Socket.Close();
        }

        private async void ClientProcessingLoop(NetworkPlayer player)
        {
            Socket s = player.Socket;
            while (s.Connected)
            {
                NetworkMessage msg = await Network.GenericReceiveMessage(s);
                if (msg == null)
                {
                    PlayerRemove(player);
                    return;
                }

                if (player.CheckIfMessageIsReply(msg)) continue;
                await GenericMessageProcessing(player, msg);
            }
        }

        public abstract Task GenericMessageProcessing(NetworkPlayer player, NetworkMessage msg);
    }

    public class Server<TNetworkGame> : Server where TNetworkGame : NetworkGame, new()
    {
        public List<TNetworkGame> Games = new();

        public Server(int port) : base(port)
        {
        }

        public override async Task GenericMessageProcessing(NetworkPlayer player, NetworkMessage msg)
        {
            if (msg.MessageType == MessageType.DropConnection)
            {
                Socket s = player.Socket;
                player.Socket = null;
                s.Close();
                Engine.Log.Warning("Disconnected", $"{player.Id}");

                if (player.Game != null)
                {
                    var droppedGame = (TNetworkGame) player.Game;
                    lock (Games)
                    {
                        Games.Remove(droppedGame);
                    }

                    await droppedGame.StopGame();
                    Engine.Log.Warning($"Game {droppedGame.Id} stopped.", "Server");

                    // Reclaim connected players from the game.
                    for (var i = 0; i < droppedGame.Players.Count; i++)
                    {
                        NetworkPlayer p = droppedGame.Players[i];
                        if (p.Socket != null) PlayerInit(p);
                    }
                }

                return;
            }

            if (msg.MessageType == MessageType.GameList)
            {
                await using var str = new MemoryStream();
                await using var writer = new BinaryWriter(str);
                lock (Games)
                {
                    writer.Write(Games.Count);
                    for (var i = 0; i < Games.Count; i++)
                    {
                        NetworkGame g = Games[i];
                        if (g.State != GameState.Waiting) continue;

                        writer.Write(g.Id);
                        writer.Write(g.Players.Count);
                        for (var j = 0; j < g.Players.Count; j++)
                        {
                            NetworkPlayer p = g.Players[j];
                            writer.Write(p.Id);
                        }
                    }
                }

                await player.SendReply(msg.CreateReply(str.ToArray()));
                return;
            }

            if (msg.MessageType == MessageType.GameJoin)
            {
                NetworkGame gameToJoin = null;
                // Create new game, or join an existing one.
                if (msg.Data == null)
                {
                    var newGame = new TNetworkGame {Host = this};
                    lock (Games)
                    {
                        Games.Add(newGame);
                        Engine.Log.Warning($"Created game {newGame.Id}.", "Server");
                    }

                    gameToJoin = newGame;
                }
                else
                {
                    string gameId = Encoding.UTF8.GetString(msg.Data);
                    lock (Games)
                    {
                        for (var i = 0; i < Games.Count; i++)
                        {
                            NetworkGame g = Games[i];
                            if (g.Id != gameId) continue;
                            gameToJoin = g;
                            break;
                        }
                    }
                }

                var joined = false;
                if (gameToJoin != null)
                {
                    joined = await gameToJoin.AddPlayer(player);
                    if (joined) player.Game = gameToJoin;
                }

                await player.SendReply(msg.CreateReply(new byte[] {joined ? 1 : 0}));
                return;
            }

            if (msg.MessageType == MessageType.WhoAmI)
            {
                Engine.Log.Info($"Player {player.Id} requested identity check.", $"{player.Socket.RemoteEndPoint}");
                byte[] stringData = Encoding.UTF8.GetBytes(player.Id);
                await player.SendReply(msg.CreateReply(stringData));
            }
        }
    }
}