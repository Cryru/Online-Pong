#region Using

using System.IO;
using System.Numerics;
using Emotion.Common;
using PongTest.Game;
using PongTest.Networking;

#endregion

namespace PongTest
{
    internal class Program
    {
        public static string DEFAULT_IP = "";
        public const int DEFAULT_PORT = 9090;

        private static Server _server;
        private static Client _client;
        private static Client _clientTw;

        private static void Main(string[] args)
        {
            if (File.Exists("./ip.txt")) DEFAULT_IP = File.ReadAllText("./ip.txt");

            var conf = new Configurator
            {
                DebugMode = true,
                RenderSize = new Vector2(960, 540),
            };
            Engine.Setup(conf);
            Engine.SceneManager.SetLoadingScreen(new PongLoadingScreen());

#if CLIENT
            _client = new PongClient(DEFAULT_IP, DEFAULT_PORT);
#else
            _server = new Server<PongNetworkGame>(DEFAULT_PORT);
            _client = new PongClient(DEFAULT_IP, DEFAULT_PORT);
            //Task.Run(() =>
            //{
            //    Task.Delay(1000).Wait();
            //    _clientTw = new PongClient(DEFAULT_IP, DEFAULT_PORT);
            //});
#endif
            Engine.Run();
        }
    }
}