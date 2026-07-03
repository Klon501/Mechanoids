using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ApexMechanoids
{
    public class DefModExtension_CelerusGas : DefModExtension
    {
        public List<ThingDef> immuneThingDefs = new List<ThingDef>();

        public bool isDoEffectWhenDamagePawn = true;

        public IntRange effectDelay = new IntRange(120, 120);

        public HediffDef hediffToImmunePawn;

        public HediffDef hediffToAffectedPawn;

        public float severityPerTrigger = 0.01f;

        public float initialSeverity = 0.1f;

        public List<FleckDef> fleckDefs;

        public EffecterDef effecterDef;

        public SoundDef soundDef;

        public float soundChance = 1f;

        public DamageDef damageDef;

        public FloatRange amount;
    }
}