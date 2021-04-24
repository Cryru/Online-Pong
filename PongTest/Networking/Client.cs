#region Using

using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Emotion.Common;
using PongTest.NetGame;

#endregion

namespace PongTest.Networking
{
    public class Client
    {
        public string Ip;
        public int Port;

        public NetworkClientServerActor Server;
        public NetworkPlayerHandle Handle;

        public DateTime LastLoopRestart;

        public Client(string defaultIp, int defaultPort)
        {
            Ip = defaultIp;
            Port = defaultPort;
            Task.Run(ConnectToServer);
        }

        private async void RunMessageLoop()
        {
            while (Server.Socket.Connected)
            {
                NetworkMessage msg = await Network.GenericReceiveMessage(Server.Socket);
                if (msg.MessageType == MessageType.DropConnection) break;
                if (!Server.CheckIfMessageIsReply(msg)) Server.AddMessageToUnprocessedQueue(msg);
            }
        }

#pragma warning disable 4014
        private async void ConnectToServer()
        {
            try
            {
                Engine.Log.Trace($"Attempting to connect to server {Ip}:{Port}...", "Client");
                var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverSocket.Connect(Ip, Port);
                Engine.Log.Trace("Connected to server!", "Client");
                Server = new NetworkClientServerActor(NetworkPlayerHandle.ServerHandle.Id)
                {
                    Socket = serverSocket,
                };

                Task.Run(RunMessageLoop);

                // Find out player id.
                NetworkMessage resp = await Server.SendMessage(new NetworkMessage
                {
                    MessageType = MessageType.WhoAmI
                });
                string id = Encoding.UTF8.GetString(resp.Data);
                Handle = new NetworkPlayerHandle(id);
                Engine.Log.Info($"Identified as player {id}", id);
                Task.Run(OnConnectedToServer);
            }
            catch (Exception ex)
            {
                Engine.Log.Error($"Client error {ex}", Handle?.Id);

                if (DateTime.Now.Subtract(LastLoopRestart).TotalSeconds < 1)
                {
                    Engine.Log.Error("Can't connect to server.", Handle?.Id ?? "Client");
                    return;
                }

                LastLoopRestart = DateTime.Now;
                Task.Run(ConnectToServer);
            }
        }
#pragma warning restore 4014

        // Run game specific logic here.
        protected virtual Task OnConnectedToServer()
        {
            return Task.CompletedTask;
        }

        protected async Task RunGameLoop(NetworkScene gameScene)
        {
            await Server.WaitForMessage(MessageType.GameStart);

            {
                NetworkMessage initialSync = await Server.WaitForMessage(MessageType.GameUpdate);
                await using var stream = new MemoryStream(initialSync.Data);
                gameScene.ReadSceneFromStream(stream);
                await Engine.SceneManager.SetScene(gameScene);

                // Confirm having received and applied the update.
                await Server.SendReply(initialSync.CreateReply());
            }

            // Wait for game loop start message.
            // It will come when everyone has confirmed their initial sync.
            await Server.WaitForMessage(MessageType.GameLoopStart);

            while (true)
            {
                NetworkMessage requestTickMessage = await Server.WaitForMessage(MessageType.RequestTick);

                await using var streamOut = new MemoryStream();
                gameScene.WriteSceneToStream(streamOut, true);
                await Server.SendReply(requestTickMessage.CreateReply(streamOut.GetBuffer()));

                NetworkMessage loopUpdateMsg = await Server.WaitForMessage(MessageType.GameUpdate);

                await using var stream = new MemoryStream(loopUpdateMsg.Data);
                gameScene.ReadSceneFromStream(stream, true);
            }
        }
    }
}