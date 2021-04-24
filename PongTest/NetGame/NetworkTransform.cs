#region Using

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Emotion.Primitives;
using Emotion.Utility;
using PongTest.Networking;

#endregion

namespace PongTest.NetGame
{
    public class NetworkTransform : Transform
    {
        public Vector3 VisualPosition;
        public string ObjectId { get; set; }
        public NetworkPlayerHandle Owner = NetworkPlayerHandle.ServerHandle;

        protected NetworkTransform()
        {
        }

        public NetworkTransform(string objId)
        {
            ObjectId = objId;
        }

        public virtual int GetDataLength()
        {
            return sizeof(float) + sizeof(float) * 3 + sizeof(float) * 2;
        }

        public void Write(Stream stream, float timestamp)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(timestamp);
            writer.Write(_x);
            writer.Write(_y);
            writer.Write(_z);
            writer.Write(_width);
            writer.Write(_height);
            WriteInternal(writer);
        }

        public void Read(Stream stream, bool forceUpdate = false)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            float time = reader.ReadSingle();
            _x = reader.ReadSingle();
            _y = reader.ReadSingle();
            _z = reader.ReadSingle();
            _width = reader.ReadSingle();
            _height = reader.ReadSingle();
            ReadInternal(reader);

            if (forceUpdate)
            {
                FastForwardInterpolation();
            }
            else
            {
                _interpolationQueue ??= new Queue<ControlPoint>();
                ControlPoint cp = _cpPool.Get();
                cp.Position = Position;
                cp.Time = time;
                _interpolationQueue.Enqueue(cp);
            }
        }

        protected virtual void WriteInternal(BinaryWriter reader)
        {
        }

        protected virtual void ReadInternal(BinaryReader reader)
        {
        }

        #region Interpolation

        private static ObjectPool<ControlPoint> _cpPool = new();

        public class ControlPoint
        {
            public Vector3 Position;
            public float Time;
        }

        private Queue<ControlPoint> _interpolationQueue;
        private ControlPoint _lastAppliedCp;
        private float _timeSinceApplied;

        public void InitializeInterpolation()
        {
            OnMove += NetworkTransform_OnMove;
            FastForwardInterpolation();
        }

        private void NetworkTransform_OnMove(object sender, EventArgs e)
        {
            FastForwardInterpolation();
        }

        private void FastForwardInterpolation()
        {
            if (_interpolationQueue != null)
            {
                foreach (ControlPoint cp in _interpolationQueue)
                {
                    _cpPool.Return(cp);
                }

                _interpolationQueue.Clear();
                if (_lastAppliedCp != null) _cpPool.Return(_lastAppliedCp);
            }

            VisualPosition = Position;
            _lastAppliedCp = null;
        }

        private void ApplyControlPoint(ControlPoint p)
        {
            VisualPosition = p.Position;
            if (_lastAppliedCp != null) _cpPool.Return(_lastAppliedCp);
            _lastAppliedCp = p;
            _timeSinceApplied = 0;
        }

        public void Update(float delta)
        {
            if (_interpolationQueue == null || _interpolationQueue.Count == 0) return;
            if (_lastAppliedCp == null)
            {
                ControlPoint cp = _interpolationQueue.Dequeue();
                ApplyControlPoint(cp);
                if (_interpolationQueue.Count == 0) return;
            }

            _timeSinceApplied += delta;

            ControlPoint nextCp = _interpolationQueue.Peek();
            float currentTime = _lastAppliedCp.Time + _timeSinceApplied;
            while (currentTime >= nextCp.Time)
            {
                ApplyControlPoint(nextCp);
                _interpolationQueue.Dequeue();
                if (_interpolationQueue.Count == 0) return;
                nextCp = _interpolationQueue.Peek();
            }

            _timeSinceApplied = currentTime - _lastAppliedCp.Time;
            float timeBetweenPoints = nextCp.Time - _lastAppliedCp.Time;
            float progress = Maths.Clamp01(_timeSinceApplied / timeBetweenPoints);
            VisualPosition = Vector3.Lerp(_lastAppliedCp.Position, nextCp.Position, progress);
        }

        #endregion
    }
}