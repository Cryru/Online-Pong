namespace PongTest.Networking
{
    public class NetworkPlayerHandle
    {
        public static NetworkPlayerHandle ServerHandle = new("Server");

        public string Id { get; }

        public NetworkPlayerHandle()
        {
            Id = Network.GenerateId("Player");
        }

        public NetworkPlayerHandle(string id)
        {
            Id = id;
        }

        public override string ToString()
        {
            return $"NetHandle: {Id}";
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as NetworkPlayerHandle);
        }

        public bool Equals(NetworkPlayerHandle obj)
        {
            if (obj == null) return false;
            return obj.Id == Id;
        }

        public static bool operator ==(NetworkPlayerHandle lhs, NetworkPlayerHandle rhs)
        {
            if (lhs is not null) return lhs.Equals(rhs);
            return rhs is null;
        }

        public static bool operator !=(NetworkPlayerHandle lhs, NetworkPlayerHandle rhs)
        {
            return !(lhs == rhs);
        }
    }
}