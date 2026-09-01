using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ApexMechanoids
{
    [StaticConstructorOnStartup]
    internal static class MechWorkTabCompat
    {
        private const string MechTabWorkerTypeName = "SM_MechTab.SM_PawnColumnWorker_WorkPriority";

        private const string ColumnDefNamePrefix = "APM_MechWorkPriority_";

        private static readonly string[] MechTabTableDefNames =
        {
            "SM_PawnTable_MechsWork",
            "SM_PawnTable_MechsWorkSlim"
        };

        private static readonly HashSet<PawnColumnDef> GeneratedColumns = new HashSet<PawnColumnDef>();

        internal static bool HasGeneratedColumns => GeneratedColumns.Count > 0;

        static MechWorkTabCompat()
        {
            Type workerClass = GenTypes.GetTypeInAnyAssembly(MechTabWorkerTypeName);
            if (workerClass == null)
            {
                return;
            }

            ModContentPack content = LoadedModManager.GetMod<ApexMechanoidsMod>()?.Content;
            if (content == null)
            {
                return;
            }

            List<PawnColumnDef> columns = BuildColumns(content, workerClass);
            if (columns.Count == 0)
            {
                return;
            }

            for (int i = 0; i < MechTabTableDefNames.Length; i++)
            {
                InsertColumns(DefDatabase<PawnTableDef>.GetNamed(MechTabTableDefNames[i], false), columns);
            }
        }

        internal static bool IsGeneratedColumn(PawnColumnDef column)
        {
            return column != null && GeneratedColumns.Count > 0 && GeneratedColumns.Contains(column);
        }

        private static List<PawnColumnDef> BuildColumns(ModContentPack content, Type workerClass)
        {
            HashSet<WorkTypeDef> mechEnabledWorkTypes = CollectMechEnabledWorkTypes();
            List<PawnColumnDef> columns = new List<PawnColumnDef>();
            bool moveWorkTypeLabelDown = false;
            foreach (WorkTypeDef workType in WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder)
            {
                if (workType.visible || workType.modContentPack != content || !mechEnabledWorkTypes.Contains(workType))
                {
                    continue;
                }

                string columnDefName = ColumnDefNamePrefix + workType.defName;
                if (DefDatabase<PawnColumnDef>.GetNamed(columnDefName, false) != null)
                {
                    continue;
                }

                PawnColumnDef column = new PawnColumnDef
                {
                    defName = columnDefName,
                    workType = workType,
                    workerClass = workerClass,
                    moveWorkTypeLabelDown = moveWorkTypeLabelDown,
                    sortable = true,
                    modContentPack = content
                };
                DefGenerator.AddImpliedDef(column);
                GeneratedColumns.Add(column);
                columns.Add(column);
                moveWorkTypeLabelDown = !moveWorkTypeLabelDown;
            }

            return columns;
        }

        private static HashSet<WorkTypeDef> CollectMechEnabledWorkTypes()
        {
            HashSet<WorkTypeDef> result = new HashSet<WorkTypeDef>();
            List<ThingDef> allThingDefs = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < allThingDefs.Count; i++)
            {
                RaceProperties race = allThingDefs[i].race;
                if (race == null || !race.IsMechanoid || race.mechEnabledWorkTypes.NullOrEmpty())
                {
                    continue;
                }

                for (int j = 0; j < race.mechEnabledWorkTypes.Count; j++)
                {
                    result.Add(race.mechEnabledWorkTypes[j]);
                }
            }

            return result;
        }

        private static void InsertColumns(PawnTableDef table, List<PawnColumnDef> columns)
        {
            if (table == null || table.columns == null)
            {
                return;
            }

            int index = table.columns.FindIndex(c => c.Worker is PawnColumnWorker_CopyPasteWorkPriorities);
            if (index < 0)
            {
                return;
            }

            for (int i = 0; i < columns.Count; i++)
            {
                if (table.columns.Contains(columns[i]) || table.columns.Exists(c => c.workType == columns[i].workType))
                {
                    continue;
                }

                table.columns.Insert(++index, columns[i]);
            }
        }
    }
}
