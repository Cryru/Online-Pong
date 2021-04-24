#region Using

using System;
using System.Numerics;
using Emotion.Common;
using Emotion.Game.Text;
using Emotion.Game.Time;
using Emotion.Graphics;
using Emotion.Graphics.Camera;
using Emotion.IO;
using Emotion.Platform.Input;
using Emotion.Primitives;
using Emotion.Utility;
using PongTest.NetGame;
using PongTest.Networking;

#endregion

namespace PongTest.Game
{
    public class PongGameScene : NetworkScene
    {
        public PongGameScene(NetworkPlayerHandle networkHandle) : base(networkHandle)
        {
        }

        public PongGameScene(NetworkGame game) : base(game)
        {
        }

        private Vector3 _pad1StartingPos;
        private Vector3 _pad2StartingPos;
        private Vector3 _ballStartingPos;

        public override void LoadServer(NetSceneDeltaState deltaScene)
        {
            float startOffset = 30;
            float ballSize = 18;
            var defaultPaddleSize = new Vector2(18, 70);
            Vector2 worldSize = Engine.Configuration.RenderSize;

            NetworkPlayer p1 = ServerGame.Players[0];
            NetworkPlayer p2 = ServerGame.Players[1];

            _pad1StartingPos = new Vector3(startOffset, worldSize.Y / 2 - defaultPaddleSize.Y / 2, 0);
            _pad1 = new PongPaddle("PaddleOne")
            {
                Position = _pad1StartingPos,
                Size = defaultPaddleSize,
                Owner = new NetworkPlayerHandle(p1.Id),
            };
            AddObject(_pad1);

            _pad2StartingPos = new Vector3(worldSize.X - startOffset - defaultPaddleSize.X, worldSize.Y / 2 - defaultPaddleSize.Y / 2, 0);
            _pad2 = new PongPaddle("PaddleTwo")
            {
                Position = _pad2StartingPos,
                Size = defaultPaddleSize,
                Owner = new NetworkPlayerHandle(p2.Id),
            };
            AddObject(_pad2);

            _ballStartingPos = new Vector3(worldSize.X / 2 - ballSize / 2, worldSize.Y / 2 - ballSize / 2, 0);
            var ball = new PongBall("Ball")
            {
                Position = _ballStartingPos,
                Size = new Vector2(ballSize)
            };
            AddObject(ball);

            var upperWall = new NetworkTransform("UpperWall")
            {
                Position = new Vector3(-100, 0, 0),
                Size = new Vector2(worldSize.X + 200, 10)
            };
            var lowerWall = new NetworkTransform("LowerWall")
            {
                Position = new Vector3(-100, worldSize.Y - 10, 0),
                Size = new Vector2(worldSize.X + 200, 10)
            };
            AddObject(upperWall);
            AddObject(lowerWall);

            _betweenMatchTimer = new After(250);
            _betweenMatchTimer.End();

            base.LoadServer(deltaScene);
        }

        private bool _p1Turn = true;
        private After _betweenMatchTimer;
        private float _ballSpeed = 0.25f;

        public override void PreServerUpdate(NetSceneDeltaState deltaState)
        {
            deltaState[_pad1] = NetworkTransformReadOperation.Update;
            deltaState[_pad2] = NetworkTransformReadOperation.Update;
        }

        public override void PostLoadServer(NetSceneDeltaState deltaState)
        {
            for (var i = 0; i < SyncedObjects.Count; i++)
            {
                deltaState[SyncedObjects[i]] = NetworkTransformReadOperation.Update;
            }

            // These walls dont move, so no need to send information about them.
            deltaState[IdToObject["UpperWall"]] = NetworkTransformReadOperation.Skip;
            deltaState[IdToObject["LowerWall"]] = NetworkTransformReadOperation.Skip;
        }

        private Vector3 CollideWithTransform(ref Rectangle ballBound, ref Vector3 vel, Transform obj)
        {
            Rectangle objBound = obj.Bounds;
            if (!ballBound.Intersects(objBound)) return Vector3.Zero;

            LineSegment[] segments;
            if (obj is PongPaddle pd)
                segments = pd.GetPaddleCollision();
            else
                segments = objBound.GetLineSegments();

            float segmentBestWeight = 0;
            ref LineSegment bestSegment = ref segments[0];

            LineSegment[] ballSegments = ballBound.GetLineSegments();

            for (var s = 0; s < segments.Length; s++)
            {
                ref LineSegment segment = ref segments[s];
                if (segment.GetIntersectionPoint(ref ballBound) == Vector2.Zero) continue;

                // Project ball onto the segment to calculate overlap.
                Vector2 lineVector = Vector2.Normalize(segment.Start - segment.End);
                float surfaceOverlapS = Vector2.Dot(lineVector, segment.Start);
                float surfaceOverlapE = Vector2.Dot(lineVector, segment.End);

                float maxSurface = MathF.Max(surfaceOverlapS, surfaceOverlapE);
                float minSurface = MathF.Min(surfaceOverlapS, surfaceOverlapE);

                var max = float.MinValue;
                var min = float.MaxValue;
                for (var i = 0; i < ballSegments.Length; i++)
                {
                    float overlapS = Vector2.Dot(lineVector, ballSegments[i].Start);
                    float overlapE = Vector2.Dot(lineVector, ballSegments[i].End);
                    max = MathF.Max(overlapS, max);
                    max = MathF.Max(overlapE, max);

                    min = MathF.Min(overlapS, min);
                    min = MathF.Min(overlapE, min);
                }

                float overlap = Maths.Get1DIntersectionDepth(min, max, minSurface, maxSurface);

                if (!(overlap > segmentBestWeight)) continue;
                segmentBestWeight = overlap;
                bestSegment = ref segment;
            }

            // Reflect only off of the segment with the most overlap in the colliding object.
            if (segmentBestWeight == 0) return Vector3.Zero;
            // Find normal of segment.
            bool? leftOf = bestSegment.IsPointLeftOf(ballBound.Center);
            if (leftOf == null) return Vector3.Zero;
            Vector2 normal = bestSegment.GetNormal(!leftOf.Value);

            Vector2 v = vel.ToVec2();
            return ((2 * Vector2.Dot(v, normal) * normal - v) * -1).ToVec3(); // Equivalent to Vector3.Reflect
        }

        public override void UpdateServer(float delta, NetSceneDeltaState deltaState)
        {
            base.UpdateServer(delta, deltaState);

            if (!_pad1.Ready || !_pad2.Ready) return;
            _betweenMatchTimer.Update(delta);
            if (!_betweenMatchTimer.Finished) return;

            // Start game.
            var ball = (PongBall) IdToObject["Ball"];
            deltaState[ball] = NetworkTransformReadOperation.Update;
            if (ball.Velocity == Vector3.Zero) ball.Velocity = Vector3.Normalize(new Vector3(_p1Turn ? -1 : 1, Helpers.GenerateRandomNumber(-30, 30) / 100.0f, 0));
            // Move ball.
            Rectangle ballBound = ball.Bounds;
            ballBound.X += ball.Velocity.X * _ballSpeed * delta;
            ballBound.Y += ball.Velocity.Y * _ballSpeed * delta;

            // Resolve collision
            Vector3 ballVelocity = ball.Velocity;
            for (var i = 0; i < SyncedObjects.Count; i++)
            {
                NetworkTransform obj = SyncedObjects[i];
                if (obj == ball) continue;
                Vector3 newVel = CollideWithTransform(ref ballBound, ref ballVelocity, obj);
                if (newVel == Vector3.Zero) continue;
                ballBound = ball.Bounds;
                if (obj is PongPaddle) _ballSpeed = MathF.Min(_ballSpeed + 0.05f, 0.8f);
                ball.Velocity = newVel;
                ballBound.X += ball.Velocity.X * _ballSpeed * delta;
                ballBound.Y += ball.Velocity.Y * _ballSpeed * delta;
                break;
            }

            if (MathF.Abs(ball.Velocity.Y) > MathF.Abs(ball.Velocity.X))
                ball.Velocity = new Vector3(ball.Velocity.Y * MathF.Sign(ball.Velocity.X), MathF.Sign(ball.Velocity.X) * MathF.Sign(ball.Velocity.Y), 0);
            ball.Bounds = ballBound;

            // Check if anyone has scored.
            var restart = false;
            if (ball.X < 0)
            {
                _pad2.Score++;
                restart = true;
                _p1Turn = true;
            }
            else if (ball.X > Engine.Configuration.RenderSize.X)
            {
                _pad1.Score++;
                restart = true;
                _p1Turn = false;
            }

            if (restart)
            {
                _pad1.Position = _pad1StartingPos;
                _pad2.Position = _pad2StartingPos;
                ball.Position = _ballStartingPos;
                ball.Velocity = Vector3.Zero;
                _betweenMatchTimer.Restart();
                _ballSpeed = 0.2f;

                // Force update all objects, because they teleported.
                deltaState[_pad1] = NetworkTransformReadOperation.UpdateForce;
                deltaState[_pad2] = NetworkTransformReadOperation.UpdateForce;
                deltaState[ball] = NetworkTransformReadOperation.UpdateForce;
            }
        }

        private PongPaddle _myPaddle;
        private FontAsset _font;
        private PongPaddle _pad1;
        private PongPaddle _pad2;

        public override void Load()
        {
            base.Load();
            _pad1 = (PongPaddle) IdToObject["PaddleOne"];
            _pad2 = (PongPaddle) IdToObject["PaddleTwo"];
            _myPaddle = _pad1.Owner == NetworkHandle ? _pad1 : _pad2;
            _font = Engine.AssetLoader.Get<FontAsset>("calibrib.ttf");
            Engine.Renderer.Camera = new TrueScaleCamera((Engine.Configuration.RenderSize / 2.0f).ToVec3());
        }

        public override void Update()
        {
            float yMod = 0;
            if (Engine.Host.IsKeyHeld(Key.W)) yMod -= 1;
            if (Engine.Host.IsKeyHeld(Key.S)) yMod += 1;
            _myPaddle.Position += new Vector3(0, yMod, 0) * 0.3f * Engine.DeltaTime;
            _myPaddle.Y = Maths.Clamp(_myPaddle.Y, 0, Engine.Configuration.RenderSize.Y - _myPaddle.Height);

            if (!_myPaddle.Ready && Engine.Host.IsKeyDown(Key.Space)) _myPaddle.Ready = true;

            for (var i = 0; i < SyncedObjects.Count; i++)
            {
                SyncedObjects[i].Update(Engine.DeltaTime);
            }
        }

        public override void Draw(RenderComposer composer)
        {
            DrawableFontAtlas atlas = _font.GetAtlas(30);
            var l = new TextLayouter(atlas.Atlas);
            string text;
            if (!_myPaddle.Ready)
                text = "Press 'Space' when ready!";
            else if (_pad1 == _myPaddle && !_pad2.Ready || _pad2 == _myPaddle && !_pad1.Ready)
                text = "Waiting for other player.";
            else
                text = $"{_pad1.Score}/{_pad2.Score}";

            Vector2 textSize = l.MeasureString(text);
            float screenHorizontalCenter = Engine.Configuration.RenderSize.X / 2;
            composer.RenderString(new Vector3(screenHorizontalCenter - textSize.X / 2, 10, 0), Color.White, text, atlas);

            composer.RenderSprite(_pad1.VisualPosition, _pad1.Size, _pad1.Ready ? Color.White : Color.Red);
            composer.RenderSprite(_pad2.VisualPosition, _pad2.Size, _pad2.Ready ? Color.White : Color.Red);

            NetworkTransform ball = IdToObject["Ball"];
            composer.RenderSprite(ball.VisualPosition, ball.Size, Color.White);

            NetworkTransform upperWall = IdToObject["UpperWall"];
            NetworkTransform lowerWall = IdToObject["LowerWall"];

            composer.RenderSprite(upperWall.VisualPosition, upperWall.Size, Color.White);
            composer.RenderSprite(lowerWall.VisualPosition, lowerWall.Size, Color.White);
        }

        public override void Unload()
        {
        }
    }
}