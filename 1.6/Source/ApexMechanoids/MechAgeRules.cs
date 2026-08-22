using RimWorld;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// How old a mech this mod hands the player is when it turns up.
    ///
    /// Left alone, <c>PawnGenerator.GenerateRandomAge</c> gives a mechanoid with no
    /// <c>ageGenerationCurve</c> a flat <c>Rand.Range(0f, 2500f)</c> years, so a container could open
    /// onto a machine that read as older than the complex it was found in. Vanilla's own
    /// <c>ScenPart_StartingMech</c> goes the other way and asks for a newborn, which comes out at
    /// zero. Neither number is one anybody chose.
    ///
    /// Everything this mod starts the player with now lands in the same band instead.
    /// </summary>
    public static class MechAgeRules
    {
        public const float MinYears = 3f;

        public const float MaxYears = 10f;

        /// <summary>The band's own random pick, kept in one place so both spawn routes agree.</summary>
        public static float RandomAgeYears()
        {
            return Rand.Range(MinYears, MaxYears);
        }

        /// <summary>
        /// A generation request for a mech of this kind, aged into the band.
        ///
        /// Biological and chronological are set to the same value on purpose. A mech that was sealed
        /// in a container has not been running all that time, and leaving chronological unset sends
        /// the generator down the cryptosleep branch, which can hand back a machine whose two ages
        /// are centuries apart for no reason the player can see.
        /// </summary>
        public static PawnGenerationRequest RequestFor(PawnKindDef kindDef, Faction faction)
        {
            float years = RandomAgeYears();
            return new PawnGenerationRequest(
                kindDef,
                faction,
                fixedBiologicalAge: years,
                fixedChronologicalAge: years);
        }
    }
}
