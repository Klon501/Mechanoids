using RimWorld;
using Verse;
using Verse.AI;

namespace ApexMechanoids
{
    public class WorkGiver_SirenWardenChat : WorkGiver_Warden
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !SirenWardenUtility.CanSirenWork(pawn) || base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return t is Pawn prisoner && ShouldTakeCareOfPrisoner(pawn, prisoner, forced) && SirenWardenUtility.CanChatWithPrisoner(pawn, prisoner, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return HasJobOnThing(pawn, t, forced) ? JobMaker.MakeJob(ApexDefsOf.APM_SirenChatWithPrisoner, t) : null;
        }
    }

    public class WorkGiver_SirenWardenReleasePrisoner : WorkGiver_Warden_ReleasePrisoner
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !SirenWardenUtility.CanSirenWork(pawn) || base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) && base.HasJobOnThing(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) ? base.JobOnThing(pawn, t, forced) : null;
        }
    }

    public class WorkGiver_SirenWardenTakeToBed : WorkGiver_Warden_TakeToBed
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !SirenWardenUtility.CanSirenWork(pawn) || base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) && base.HasJobOnThing(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) ? base.JobOnThing(pawn, t, forced) : null;
        }
    }

    public class WorkGiver_SirenWardenFeed : WorkGiver_Warden_Feed
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !SirenWardenUtility.CanSirenWork(pawn) || base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) && base.HasJobOnThing(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) ? base.JobOnThing(pawn, t, forced) : null;
        }
    }

    public class WorkGiver_SirenWardenDeliverFood : WorkGiver_Warden_DeliverFood
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !SirenWardenUtility.CanSirenWork(pawn) || base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) && base.HasJobOnThing(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) ? base.JobOnThing(pawn, t, forced) : null;
        }
    }

    public class WorkGiver_SirenWardenDoExecution : WorkGiver_Warden_DoExecution
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !SirenWardenUtility.CanSirenWork(pawn) || base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) && base.HasJobOnThing(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) ? base.JobOnThing(pawn, t, forced) : null;
        }
    }

    public class WorkGiver_SirenWardenExecuteSlave : WorkGiver_Warden_ExecuteSlave
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !ModsConfig.IdeologyActive || !SirenWardenUtility.CanSirenWork(pawn) || base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) && base.HasJobOnThing(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) ? base.JobOnThing(pawn, t, forced) : null;
        }
    }

    public class WorkGiver_SirenWardenEmancipateSlave : WorkGiver_Warden_EmancipateSlave
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !ModsConfig.IdeologyActive || !SirenWardenUtility.CanSirenWork(pawn) || base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) && base.HasJobOnThing(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) ? base.JobOnThing(pawn, t, forced) : null;
        }
    }

    public class WorkGiver_SirenWardenImprisonSlave : WorkGiver_Warden_ImprisonSlave
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !ModsConfig.IdeologyActive || !SirenWardenUtility.CanSirenWork(pawn) || base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) && base.HasJobOnThing(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return SirenWardenUtility.CanSirenWork(pawn) ? base.JobOnThing(pawn, t, forced) : null;
        }
    }

    public class WorkGiver_SirenWardenEnslave : WorkGiver_Warden
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !ModsConfig.IdeologyActive || !SirenWardenUtility.CanSirenWork(pawn) || base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return t is Pawn prisoner && ShouldTakeCareOfPrisoner(pawn, prisoner, forced) && SirenWardenUtility.CanEnslavePrisoner(pawn, prisoner, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!HasJobOnThing(pawn, t, forced))
            {
                return null;
            }

            Pawn prisoner = (Pawn)t;
            JobDef jobDef = prisoner.guest.IsInteractionEnabled(PrisonerInteractionModeDefOf.Enslave) ? ApexDefsOf.APM_SirenEnslavePrisoner : ApexDefsOf.APM_SirenReduceWillPrisoner;
            return JobMaker.MakeJob(jobDef, t);
        }
    }

    public class WorkGiver_SirenWardenConvert : WorkGiver_Warden
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !ModsConfig.IdeologyActive || !SirenWardenUtility.CanSirenWork(pawn) || base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return t is Pawn prisoner && ShouldTakeCareOfPrisoner(pawn, prisoner, forced) && SirenWardenUtility.CanConvertPrisoner(pawn, prisoner, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return HasJobOnThing(pawn, t, forced) ? JobMaker.MakeJob(ApexDefsOf.APM_SirenConvertPrisoner, t) : null;
        }
    }

    public class WorkGiver_SirenWardenSuppressSlave : WorkGiver_Warden
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !ModsConfig.IdeologyActive || !SirenWardenUtility.CanSirenWork(pawn) || base.ShouldSkip(pawn, forced);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return t is Pawn slave && ShouldTakeCareOfSlave(pawn, slave, forced) && SirenWardenUtility.CanSuppressSlave(pawn, slave, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return HasJobOnThing(pawn, t, forced) ? JobMaker.MakeJob(ApexDefsOf.APM_SirenSuppressSlave, t) : null;
        }
    }
}
