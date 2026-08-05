using System.Collections.Generic;
using Verse;

namespace ApexMechanoids
{
    // Everything the Aegis passive needs, in one place. Put it under <comps> on a mech's ThingDef
    // and that mech gets the shield behaviour: attacks coming in from the front or the sides are
    // caught by the shield on that side, destroyed shields slowly rebuild themselves once the mech
    // is left alone, and the player sees a shield integrity readout.
    public class CompProperties_Aegis : CompProperties
    {
        // The body part that represents a shield, and the left/right groups it belongs to.
        public BodyPartDef shieldPart;
        public BodyPartGroupDef leftShieldGroup;
        public BodyPartGroupDef rightShieldGroup;

        // Shield self-regeneration (kept intentionally very slow). The comp runs on CompTickRare,
        // so an interval below ~4.16s rounds up to that.
        public float regenerationDelaySeconds = 60f;
        public float regenerationIntervalSeconds = 30f;
        public float regenerationHPPerStep = 1f;

        // Chance an attack from that direction is caught by a shield. Attacks from behind always
        // get through.
        public float frontDamageChance = 1f;
        public float sideDamageChance = 0.2f;

        // Extra mech energy drained per shield HP restored during a repair job, scaled by the
        // mech's MechEnergyLossPerHP stat. Higher = shields cost more to repair.
        public float repairEnergyCostMultiplier = 3f;

        public CompProperties_Aegis()
        {
            compClass = typeof(CompAegis);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (shieldPart == null)
            {
                yield return "CompProperties_Aegis needs a shieldPart.";
            }

            if (leftShieldGroup == null && rightShieldGroup == null)
            {
                yield return "CompProperties_Aegis needs at least one of leftShieldGroup/rightShieldGroup.";
            }
        }
    }
}
