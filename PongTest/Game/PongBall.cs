#region Using

using System.Numerics;
using PongTest.NetGame;

#endregion

namespace PongTest.Game
{
    public class PongBall : NetworkTransform
    {
        public Vector3 Velocity;

        public PongBall(string objId) : base(objId)
        {
            ObjectId = objId;
        }

        protected PongBall()
        {
        }
    }
}