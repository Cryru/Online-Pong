#region Using

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

#endregion

namespace PongTest.Networking
{
    public enum MessageType : byte
    {
        // Errors
        Error = 0,
        DropConnection = 1,

        // Generic
        WhoAmI = 2,

        // Game Lobby
        GameList = 3,
        GameJoin = 4,
        GameStart = 5,
        GameLoopStart = 6,
        GameStop = 7,

        // Game
        RequestTick = 10,
        GameUpdate = 11
    }

    public class NetworkMessage
    {
        public const int BASE_LENGTH = 9;
        private static int _nextMessageIdentifier = 256;

        public MessageType MessageType;
        public int MessageIdentifier;
        public byte[] Data { get; private set; }

        public NetworkMessage(bool dontGenerateIdentifier, byte[] data)
        {
            Data = data;
        }

        public NetworkMessage()
        {
            MessageIdentifier = Interlocked.Increment(ref _nextMessageIdentifier);
        }

        public NetworkMessage(byte[] dataAllocated) : this()
        {
            Data = dataAllocated;
        }

        public NetworkMessage(string str) : this()
        {
            Data = Encoding.UTF8.GetBytes(str);
        }

        public void Write(Stream stream)
        {
            using var writer = new BinaryWriter(stream);

            writer.Write((byte) MessageType);
            writer.Write(MessageIdentifier);
            if (Data != null)
            {
                writer.Write(Data.Length);
                writer.Write(Data);
            }
            else
            {
                writer.Write(0); // Int (4 bytes)
            }
        }

        public void Read(Stream stream)
        {
            using var reader = new BinaryReader(stream);
            MessageType = (MessageType) reader.ReadByte();
            MessageIdentifier = reader.ReadInt32();
            int dataSize = reader.ReadInt32();
            if (dataSize == 0) return;
            if (Data != null && Data.Length > dataSize)
                reader.Read(Data, 0, dataSize);
            else
                Data = reader.ReadBytes(dataSize);
        }

        protected NetworkMessage(int messageId)
        {
            MessageIdentifier = messageId;
        }

        public NetworkMessage CreateReply(byte[] data)
        {
            return new(MessageIdentifier)
            {
                Data = data,
                MessageType = MessageType,
            };
        }

        public NetworkMessage CreateReply()
        {
            return new(MessageIdentifier)
            {
                MessageType = MessageType,
            };
        }

        public override string ToString()
        {
            return $"{MessageType} #{Data?.Length ?? 0}";
        }
    }
}