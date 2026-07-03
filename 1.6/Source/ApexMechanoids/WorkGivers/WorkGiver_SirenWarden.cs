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
}
