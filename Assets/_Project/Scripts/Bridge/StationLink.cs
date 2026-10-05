using UnityEngine;

namespace StarTrek.Bridge
{
    /// <summary>Ties a seat (or any prop) to the station it belongs to, for looking-at and sitting-at checks.</summary>
    public class StationLink : MonoBehaviour
    {
        [SerializeField] StationConsole console;

        public StationConsole Console => console;

        /// <summary>The station a collider belongs to, directly or through a link.</summary>
        public static StationConsole Resolve(Component c)
        {
            if (c == null)
                return null;
            var console = c.GetComponentInParent<StationConsole>();
            if (console != null)
                return console;
            var link = c.GetComponentInParent<StationLink>();
            return link != null ? link.console : null;
        }
    }
}
