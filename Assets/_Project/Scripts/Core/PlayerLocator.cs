using UnityEngine;

namespace StarTrek.Core
{
    /// <summary>Lets world objects (doors, triggers) find the player without searching the scene.</summary>
    public static class PlayerLocator
    {
        public static Transform Player { get; private set; }

        public static void Register(Transform player) => Player = player;

        public static void Unregister(Transform player)
        {
            if (Player == player)
                Player = null;
        }
    }
}
