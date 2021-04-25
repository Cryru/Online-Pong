#region Using

using System.IO;
using System.Numerics;
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

        private LineSegment[] _collision = new LineSegment[6];
        public LineSegment[] GetPaddleCollision()
        {
            _collision[0] = new LineSegment(new Vector2(X, Y), new Vector2(X, Y + Height));
            _collision[1] = new LineSegment(new Vector2(X + Width, Y), new Vector2(X + Width, Y + Height));

            _collision[2] = new LineSegment(new Vector2(X, Y), new Vector2(X + 5, Y - 10));
            _collision[3] = new LineSegment(new Vector2(X + Width, Y), new Vector2(X + Width - 5, Y - 10));

            _collision[4] = new LineSegment(new Vector2(X, Y + Height), new Vector2(X + 5, Y + Height + 10));
            _collision[5] = new LineSegment(new Vector2(X + Width, Y + Height), new Vector2(X + Width - 5, Y + Height + 10));

            return _collision;
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