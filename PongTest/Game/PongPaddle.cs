#region Using

using System.IO;
using System.Numerics;
using Emotion.Common;
using Emotion.Primitives;
using PongTest.NetGame;

#endregion

namespace PongTest.Game
{
    public class PongPaddle : NetworkTransform
    {
        public bool Ready;
        public int Score;

        public PongPaddle(string objId) : base(objId)
        {
            ObjectId = objId;
        }

        protected PongPaddle()
        {

        }

        private static float _incline = 20;
        private LineSegment[] _collision = new LineSegment[3];
        public LineSegment[] GetPaddleCollision()
        {
            bool leftPaddle = X < Engine.Configuration.RenderSize.X / 2;

            if (leftPaddle)
            {
                _collision[0] = new LineSegment(new Vector2(X + Width, Y + _incline), new Vector2(X + Width, Y + Height - _incline));
                _collision[1] = new LineSegment(new Vector2(X + Width - _incline / 2, Y), new Vector2(X + Width, Y + _incline));
                _collision[2] = new LineSegment(new Vector2(X + Width - _incline / 2, Y + Height), new Vector2(X + Width, Y + Height - _incline));
            }
            else
            {
                _collision[0] = new LineSegment(new Vector2(X, Y + _incline), new Vector2(X, Y + Height - _incline));
                _collision[1] = new LineSegment(new Vector2(X + _incline / 2, Y), new Vector2(X, Y + _incline));
                _collision[2] = new LineSegment(new Vector2(X + _incline / 2, Y + Height), new Vector2(X, Y + Height - _incline));
            }

            return _collision;
        }

        public Rectangle GetPaddleBound()
        {
            Rectangle b = Bounds;
            b.Y += _incline;
            b.Height -= _incline * 2;
            return b;
        }

        public override int GetDataLength()
        {
            return base.GetDataLength() + 1 + 4;
        }

        protected override void WriteInternal(BinaryWriter reader)
        {
            reader.Write(Ready);
            reader.Write(Score);
        }

        protected override void ReadInternal(BinaryReader reader)
        {
            Ready = reader.ReadBoolean();
            Score = reader.ReadInt32();
        }
    }
}