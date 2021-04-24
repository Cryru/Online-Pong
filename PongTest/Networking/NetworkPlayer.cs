namespace PongTest.Networking
{
    public class NetworkPlayer : NetworkActor
    {
        public NetworkGame Game;

        public NetworkPlayer()
        {
        }

        public NetworkPlayer(string id) : base(id)
        {
        }
    }
}