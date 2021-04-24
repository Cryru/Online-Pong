#region Using

using System.Numerics;
using Emotion.Common;
using Emotion.Graphics;
using Emotion.IO;
using Emotion.Primitives;
using Emotion.Scenography;

#endregion

namespace PongTest.Game
{
    public class PongLoadingScreen : IScene
    {
        private FontAsset _font;

        public void Load()
        {
            _font = Engine.AssetLoader.Get<FontAsset>("calibrib.ttf");
        }

        public void Update()
        {
        }

        public void Draw(RenderComposer composer)
        {
            composer.SetUseViewMatrix(false);
            composer.RenderString(Vector3.Zero, Color.White, "Connecting...", _font.GetAtlas(30));
            composer.SetUseViewMatrix(true);
        }

        public void Unload()
        {
        }
    }
}