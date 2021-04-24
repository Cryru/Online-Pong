#region Using

using System;
using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Emotion.Common;

#endregion

namespace PongTest.Networking
{
    public class Network
    {
        public static byte[] ObjectMessageSeparator = {29, 69, 29};
        private static ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;

        public static async Task GenericSendMessage(Socket socket, NetworkMessage msg)
        {
            int dataSize = msg.Data?.Length ?? 0;
            int arrSize = NetworkMessage.BASE_LENGTH + dataSize;
            byte[] arr = _arrayPool.Rent(arrSize);
            try
            {
                var exactArr = new ArraySegment<byte>(arr, 0, arrSize); // ArrayPool can return a larger array than requested.
                await using var str = new MemoryStream(exactArr.Array!, exactArr.Offset, exactArr.Count);
                msg.Write(str);
                await socket.SendAsync(exactArr, SocketFlags.None);
            }
            catch (Exception ex)
            {
                Engine.Log.Error(ex.ToString(), $"SEND TO{socket.RemoteEndPoint}");
            }
            finally
            {
                _arrayPool.Return(arr);
            }
        }

        public static async Task<NetworkMessage> GenericReceiveMessage(Socket socket)
        {
            byte[] arr = null;
            try
            {
                arr = _arrayPool.Rent(NetworkMessage.BASE_LENGTH);
                int bytesReceived = await socket.ReceiveAsync(new ArraySegment<byte>(arr, 0, NetworkMessage.BASE_LENGTH), SocketFlags.None);
                if (bytesReceived < NetworkMessage.BASE_LENGTH) return null;

                byte[] dataArray = null;
                var dataLength = BitConverter.ToInt32(arr, 5);
                if (dataLength > 0)
                {
                    dataArray = new byte[dataLength];
                    bytesReceived = await socket.ReceiveAsync(dataArray, SocketFlags.None);
                    if (bytesReceived < dataLength) return null;
                }

                var msg = new NetworkMessage(true, dataArray)
                {
                    MessageType = (MessageType) arr[0],
                    MessageIdentifier = BitConverter.ToInt32(arr, 1),
                };

                return msg;
            }
            catch (Exception ex)
            {
                Engine.Log.Error(ex.ToString(), $"REC FROM{socket.RemoteEndPoint}");
                return new NetworkMessage(true, null) {MessageType = MessageType.DropConnection};
            }
            finally
            {
                if (arr != null) _arrayPool.Return(arr);
            }
        }

        private static int _idIncr;

        public static string GenerateId(string usage)
        {
            int myId = Interlocked.Increment(ref _idIncr);
            return usage[0] + Convert.ToHexString(BitConverter.GetBytes(myId));
        }
    }
}