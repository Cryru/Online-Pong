namespace PongTest.NetGame
{
    public enum NetworkTransformReadOperation : byte
    {
        Skip, // Not part of the stream.
        Add, // Followed by the object id and string denoting the type, and then Update data.
        Remove, // Followed by the object id.
        Update, // Followed by the object id and then object data.
        UpdateForce, // Same format as update
    }
}