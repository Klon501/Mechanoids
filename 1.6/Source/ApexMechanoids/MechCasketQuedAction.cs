using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    public class MechCasketQuedAction
    {

        public LocalTargetInfo targetinfo = null;

        public int action = 0;


        public MechCasketQuedAction(LocalTargetInfo targetinfo, int action)
        {
            this.targetinfo = targetinfo;
            this.action = action;
        }

    }
}
