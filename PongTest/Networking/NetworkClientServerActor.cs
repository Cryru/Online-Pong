#region Using

using System.Collections.Generic;
using System.Threading.Tasks;

#endregion

namespace PongTest.Networking
{
    public class NetworkClientServerActor : NetworkActor
    {
        private List<NetworkMessage> _msgQueue = new();

        public NetworkClientServerActor()
        {
        }

        public NetworkClientServerActor(string id) : base(id)
        {
        }

        public void AddMessageToUnprocessedQueue(NetworkMessage msg)
        {
            lock (_msgQueue)
            {
                _msgQueue.Add(msg);
            }
        }

        public override async Task<NetworkMessage> WaitForMessage(MessageType type)
        {
            lock (_msgQueue)
            {
                for (var i = 0; i < _msgQueue.Count; i++)
                {
                    NetworkMessage msg = _msgQueue[i];
                    if (msg.MessageType != type) continue;
                    _msgQueue.RemoveAt(i);
                    return msg;
                }
            }

            return await base.WaitForMessage(type);
        }

        public override void Reset()
        {
            lock (_msgQueue)
            {
                _msgQueue.Clear();
            }

            base.Reset();
        }
    }
}