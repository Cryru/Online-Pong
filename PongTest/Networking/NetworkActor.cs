#region Using

using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using Emotion.Common;

#endregion

namespace PongTest.Networking
{
    public class NetworkActor : NetworkPlayerHandle
    {
        public Socket Socket;
        private Dictionary<int, NetworkMessageReply> _awaitingReplies = new();

        public NetworkActor()
        {
        }

        public NetworkActor(string actor) : base(actor)
        {
        }

        public async Task<NetworkMessage> SendMessage(NetworkMessage msg)
        {
            int messageId = msg.MessageIdentifier;
            var replyWait = new NetworkMessageReply();
            _awaitingReplies.Add(messageId, replyWait);

            //Engine.Log.Trace($"{msg}", $"Send->{Id}");
            await Network.GenericSendMessage(Socket, msg);
            return await replyWait;
        }

        public async Task SendReply(NetworkMessage replyMsg)
        {
            await Network.GenericSendMessage(Socket, replyMsg);
        }

        public virtual async Task<NetworkMessage> WaitForMessage(MessageType type)
        {
            var id = (int) type;
            if (!_awaitingReplies.TryGetValue(id, out NetworkMessageReply replyWait))
            {
                replyWait = new NetworkMessageReply();
                _awaitingReplies.Add(id, replyWait);
            }

            return await replyWait;
        }

        public bool CheckIfMessageIsReply(NetworkMessage msg)
        {
            //Engine.Log.Trace($"{msg}", $"Receive<-{Id}");

            int msgId = msg.MessageIdentifier;
            if (_awaitingReplies.TryGetValue(msgId, out NetworkMessageReply replyWait))
            {
                _awaitingReplies.Remove(msgId);
                replyWait.ReplyEvent.SetReplyMessage(msg);
                return true;
            }

            msgId = (int) msg.MessageType;
            if (_awaitingReplies.TryGetValue(msgId, out replyWait))
            {
                _awaitingReplies.Remove(msgId);
                replyWait.ReplyEvent.SetReplyMessage(msg);
                return true;
            }

            return false;
        }

        public virtual void Reset()
        {
            lock (_awaitingReplies)
            {
                foreach (KeyValuePair<int, NetworkMessageReply> waiter in _awaitingReplies)
                {
                    waiter.Value.ReplyEvent.SetReplyMessage(null);
                }

                _awaitingReplies.Clear();
            }
        }
    }
}