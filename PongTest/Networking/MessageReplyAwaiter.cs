#region Using

using System;
using System.Runtime.CompilerServices;

#endregion

namespace PongTest.Networking
{
    public class NetworkMessageReply
    {
        public class MessageReplyAwaiter : INotifyCompletion
        {
            public bool IsCompleted
            {
                get => _reply != null;
            }

            private NetworkMessage _reply;
            private Action _callback;

            public void SetReplyMessage(NetworkMessage msg)
            {
                _reply = msg;
                _callback?.Invoke();
            }

            public void OnCompleted(Action continuation)
            {
                // Check if already completed.
                if (_reply != null)
                {
                    continuation();
                    return;
                }

                _callback = continuation;
            }

            public NetworkMessage GetResult()
            {
                return _reply;
            }
        }

        public MessageReplyAwaiter ReplyEvent = new();

        public MessageReplyAwaiter GetAwaiter()
        {
            return ReplyEvent;
        }
    }
}