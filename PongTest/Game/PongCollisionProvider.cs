#region Using

using System.Collections;
using System.Collections.Generic;
using Emotion.Game;
using Emotion.Primitives;
using PongTest.NetGame;

#endregion

namespace PongTest.Game
{
    public class PongCollisionProvider : IEnumerable<Collision.CollisionNode<NetworkTransform>>
    {
        public NetworkTransform Me;
        private Collision.CollisionNode<NetworkTransform> _nodeObj; // Reused to reduce allocations.
        private NetworkScene _scene;

        public PongCollisionProvider(NetworkScene scene)
        {
            _scene = scene;
            _nodeObj = new Collision.CollisionNode<NetworkTransform>();
        }

        public IEnumerator<Collision.CollisionNode<NetworkTransform>> GetEnumerator()
        {
            for (var e = 0; e < _scene.SyncedObjects.Count; e++)
            {
                NetworkTransform ent = _scene.SyncedObjects[e];
                if (ent == Me) continue;
                _nodeObj.Entity = ent;
                if (ent is PongPaddle paddle)
                {
                    LineSegment[] paddleSurfaces = paddle.GetPaddleCollision();
                    for (var i = 0; i < paddleSurfaces.Length; i++)
                    {
                        _nodeObj.Surface = paddleSurfaces[i];
                        yield return _nodeObj;
                    }
                }
                else
                {
                    LineSegment[] surfaces = ent.Bounds.GetLineSegments();
                    for (var i = 0; i < surfaces.Length; i++)
                    {
                        _nodeObj.Surface = surfaces[i];
                        yield return _nodeObj;
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}