#region Using

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Emotion.Common;
using Emotion.Graphics;
using Emotion.Scenography;
using Emotion.Standard.XML;
using PongTest.Networking;

#endregion

namespace PongTest.NetGame
{
    public abstract class NetworkScene : IScene
    {
        public int TicksPerSecond = 20;

        public List<NetworkTransform> SyncedObjects = new();
        public Dictionary<string, NetworkTransform> IdToObject = new();
        public List<NetworkTransform> OwnedObjects = new();

        // Only one of these two will be available.
        public NetworkPlayerHandle NetworkHandle; // On client
        public NetworkGame ServerGame; // On server

        protected NetworkScene(NetworkPlayerHandle networkHandle)
        {
            NetworkHandle = networkHandle;
        }

        protected NetworkScene(NetworkGame serverGame)
        {
            ServerGame = serverGame;
        }

        public void AddObject(NetworkTransform transform)
        {
            SyncedObjects.Add(transform);
            IdToObject.Add(transform.ObjectId, transform);
            if (transform.Owner == NetworkHandle) OwnedObjects.Add(transform);
        }

        public void RemoveObject(NetworkTransform transform)
        {
            SyncedObjects.Remove(transform);
            IdToObject.Remove(transform.ObjectId);
            OwnedObjects.Remove(transform);
        }

        public void WriteSceneToStream(Stream stream, bool ownedOnly = false, NetSceneDeltaState delta = null, float timestamp = 0)
        {
            try
            {
                List<NetworkTransform> objList = ownedOnly ? OwnedObjects : SyncedObjects;

                using var writer = new BinaryWriter(stream);
                writer.Write(objList.Count);

                for (var i = 0; i < objList.Count; i++)
                {
                    NetworkTransform obj = objList[i];

                    var op = NetworkTransformReadOperation.Update;
                    if (delta != null && delta.ContainsKey(obj)) op = delta[obj];

                    writer.Write((byte) op);
                    writer.Write(obj.ObjectId);
                    switch (op)
                    {
                        case NetworkTransformReadOperation.Add:
                            writer.Write(obj.GetType().ToString());
                            writer.Write(obj.Owner.Id);
                            break;
                        case NetworkTransformReadOperation.UpdateForce:
                        case NetworkTransformReadOperation.Update:
                            // nop
                            break;
                        case NetworkTransformReadOperation.Skip:
                        case NetworkTransformReadOperation.Remove:
                            writer.Write(Network.ObjectMessageSeparator);
                            continue;
                    }

                    int expectedDataToWrite = obj.GetDataLength();
                    long currentPtr = stream.Position;
                    obj.Write(stream, timestamp);
                    Debug.Assert(stream.Position - currentPtr == expectedDataToWrite);
                    writer.Write(Network.ObjectMessageSeparator);
                }
            }
            catch (Exception ex)
            {
                Engine.Log.Error($"Error in generating full server state. {ex}", "NetworkGame");
            }
        }

        public void ReadSceneFromStream(Stream stream, bool skipOwned = false)
        {
            try
            {
                using var reader = new BinaryReader(stream);
                int operationCount = reader.ReadInt32();

                for (var o = 0; o < operationCount; o++)
                {
                    var op = (NetworkTransformReadOperation) reader.ReadByte();
                    string objectId = reader.ReadString();

                    if (op == NetworkTransformReadOperation.Add)
                    {
                        string type = reader.ReadString();
                        string ownerId = reader.ReadString();

                        // Use XML to create object. Easier than doing the reflection ourselves and very fast.
                        var fakeXml = $"<?xml><NetworkTransform type=\"{type}\"></NetworkTransform></xml>";
                        var newObj = XMLFormat.From<NetworkTransform>(fakeXml);
                        newObj.Owner = new NetworkPlayerHandle(ownerId);
                        newObj.ObjectId = objectId;
                        AddObject(newObj);
                        op = NetworkTransformReadOperation.UpdateForce;
                    }

                    // Apply data to object.
                    if (!IdToObject.TryGetValue(objectId, out NetworkTransform obj))
                    {
                        Engine.Log.Warning($"Couldn't find object with id {objectId}", "NetworkState");
                        return;
                    }

                    switch (op)
                    {
                        case NetworkTransformReadOperation.UpdateForce:
                        case NetworkTransformReadOperation.Update:
                            int dataLength = obj.GetDataLength();
                            long curPtr = stream.Position;

                            bool forceUpdate = op == NetworkTransformReadOperation.UpdateForce;
                            if (!forceUpdate && skipOwned && obj.Owner == NetworkHandle)
                            {
                                reader.ReadBytes(dataLength);
                            }
                            else
                            {
                                obj.Read(stream, forceUpdate);
                                Debug.Assert(stream.Position - curPtr == dataLength);
                            }

                            break;
                        case NetworkTransformReadOperation.Remove:
                            RemoveObject(obj);
                            break;
                    }

                    // Check separator.
                    for (var i = 0; i < Network.ObjectMessageSeparator.Length; i++)
                    {
                        int b = stream.ReadByte();
                        if (b == -1) break; // Stream end
                        if (b != Network.ObjectMessageSeparator[i])
                        {
                            Engine.Log.Warning("Network stream separation error.", "NetworkState");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Engine.Log.Error($"Exception in applying object state {ex}", "NetworkState");
            }
        }

        public virtual void LoadServer(NetSceneDeltaState deltaState)
        {
            // By default add all objects.
            for (var i = 0; i < SyncedObjects.Count; i++)
            {
                deltaState[SyncedObjects[i]] = NetworkTransformReadOperation.Add;
            }
        }

        public virtual void PostLoadServer(NetSceneDeltaState deltaState)
        {
            deltaState.Clear();
        }

        public virtual void PreServerUpdate(NetSceneDeltaState deltaState)
        {

        }

        public virtual void UpdateServer(float delta, NetSceneDeltaState deltaState)
        {
            if (deltaState.Count > 0) return;

            // By default set all objects to update.
            for (var i = 0; i < SyncedObjects.Count; i++)
            {
                deltaState[SyncedObjects[i]] = NetworkTransformReadOperation.Update;
            }
        }

        public virtual void Load()
        {
            for (var i = 0; i < SyncedObjects.Count; i++)
            {
                SyncedObjects[i].InitializeInterpolation();
            }
        }

        public abstract void Update();
        public abstract void Draw(RenderComposer composer);
        public abstract void Unload();
    }
}