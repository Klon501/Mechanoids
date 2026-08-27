using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// One warhead a javelin's rocket rack can be loaded with.
    /// </summary>
    public class JavelinRocketOption
    {
        // Written to the save instead of the list position, so inserting or reordering a warhead
        // later does not silently rearm every javelin in an existing colony.
        public string key;

        public ThingDef projectile;
        public string label;
        public string description;

        // Drawn from the launcher's uranium magazine, per shot. Zero is free, and the first rocket
        // in the rack has to be free because it is the one everything falls back to.
        public int uraniumCost;

        // Withheld from launchers the player does not own. Mechanoid raids field javelins too, and
        // an EMP warhead in their hands would disable the player's own mechs.
        public bool playerOnly;
    }

    /// <summary>
    /// Put on a mech alongside a CompProperties_Refuelable holding its uranium to give it a
    /// switchable rocket rack. See CompJavelinRocketRack.
    /// </summary>
    public class CompProperties_JavelinRocketRack : CompProperties
    {
        // Def order is load order: the first entry is what the rack falls back to when it cannot
        // pay for anything else, and what a launcher starts with.
        public List<JavelinRocketOption> rockets = new List<JavelinRocketOption>();

        // How many shots of its rolled warhead a launcher the player does not own is stocked with
        // when it spawns. It pays for every one of them out of that magazine and drops to the basic
        // rocket once it is dry, so this is how many specialist warheads a raid gets to open with,
        // not how long it can fight. Capped by the magazine's own capacity, so an expensive warhead
        // gets fewer.
        public int enemyRocketCharges = 3;

        public CompProperties_JavelinRocketRack()
        {
            compClass = typeof(CompJavelinRocketRack);
        }
    }
}
