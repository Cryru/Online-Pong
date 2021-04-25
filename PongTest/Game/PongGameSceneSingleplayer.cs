#region Using

using Emotion.Common;
using Emotion.Graphics;
using Emotion.Scenography;
using PongTest.NetGame;
using PongTest.Networking;

#endregion

namespace PongTest.Game
{
    public class PongGameSceneSingleplayer : IScene
    {
        private NetSceneDeltaState _d = new();
        private PongGameScene _gameScene = new(new NetworkPlayerHandle("Local"));

        public void Load()
        {
            _gameScene.LoadServer(_d);
            _gameScene.PostLoadServer(_d);
            _gameScene.Load();

            var paddleOne = (PongPaddle) _gameScene.IdToObject["PaddleOne"];
            paddleOne.Ready = true;
        }

        public void Update()
        {
            _gameScene.PreServerUpdate(_d);
            _gameScene.UpdateServer(Engine.DeltaTime, _d);
            _gameScene.Update();
        }

        public void Draw(RenderComposer composer)
        {
            _gameScene.Draw(composer);
        }

        public void Unload()
        {
            _gameScene.Unload();
        }
    }
}