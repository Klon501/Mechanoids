using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ApexMechanoids
{
    /// <summary>
    /// The rocket rack on a javelin: which warhead is loaded, the gizmo that switches it, and the
    /// uranium it is paid for with.
    ///
    /// The uranium sits in a CompRefuelable on the same mech, which is the arrangement the dynamo
    /// already uses for its own uranium - it makes loading rockets ordinary hauling work, so the
    /// feature needs no job, no work giver and no bill of its own. Only the shot itself is charged;
    /// nothing is spent while the mech is standing around, and nothing is refunded when the player
    /// switches warheads, so the choice costs only what it fires.
    ///
    /// The basic rocket is free, and everything falls back to it. That is what stops an empty
    /// magazine turning the mech into a spectator: it keeps shooting, just without the warhead the
    /// player asked for, and the rack says so and reverts rather than pretending otherwise.
    ///
    /// Which rockets may be loaded at all is <see cref="JavelinRocketSelection"/>, kept free of
    /// Verse types so the rules can be checked outside the game.
    /// </summary>
    public class CompJavelinRocketRack : ThingComp
    {
        private string loadedKey;

        // A launcher the player does not own is handed one warhead the first time it spawns and
        // keeps it. Latched so reloading a save does not roll it a new one.
        private bool loadoutAssigned;

        public CompProperties_JavelinRocketRack Props => (CompProperties_JavelinRocketRack)props;

        private List<JavelinRocketOption> Rockets => Props?.rockets;

        // Resolved once: comps are built with their parent and never swapped, and this is read on
        // the verb's target-scanning path.
        private CompRefuelable magazineCache;

        private CompRefuelable Magazine => magazineCache ?? (magazineCache = parent.GetComp<CompRefuelable>());

        private bool IsPlayerLauncher => parent.Faction != null && parent.Faction.IsPlayer;

        /// <summary>
        /// The rocket everything falls back to. First in the def list, and the one that has to be
        /// free.
        /// </summary>
        public JavelinRocketOption Fallback
        {
            get
            {
                List<JavelinRocketOption> rockets = Rockets;
                return rockets == null || rockets.Count == 0 ? null : rockets[0];
            }
        }

        /// <summary>
        /// The warhead the rack is set to, whether or not it can currently be paid for.
        /// </summary>
        public JavelinRocketOption Loaded
        {
            get
            {
                List<JavelinRocketOption> rockets = Rockets;
                if (rockets == null || loadedKey == null)
                {
                    return Fallback;
                }

                for (int i = 0; i < rockets.Count; i++)
                {
                    if (rockets[i].key == loadedKey)
                    {
                        return rockets[i];
                    }
                }

                // The loaded rocket was removed from the def between saves.
                return Fallback;
            }
        }

        /// <summary>
        /// The warhead the next shot will actually leave with, which is the loaded one until the
        /// magazine can no longer pay for it.
        /// </summary>
        public JavelinRocketOption Firing
        {
            get
            {
                JavelinRocketOption loaded = Loaded;
                return loaded != null && CanFire(loaded) ? loaded : Fallback;
            }
        }

        /// <summary>
        /// The projectile the verb should launch. Null leaves the weapon on its own default.
        /// </summary>
        public ThingDef CurrentProjectile => Firing?.projectile;

        private float UraniumHeld => Magazine?.Fuel ?? 0f;

        private bool CanFire(JavelinRocketOption rocket)
        {
            return rocket != null
                   && rocket.projectile != null
                   && JavelinRocketSelection.CanFire(rocket.uraniumCost, UraniumHeld, IsPlayerLauncher);
        }

        private bool CanSelect(JavelinRocketOption rocket)
        {
            return JavelinRocketSelection.CanSelect(rocket.playerOnly, IsPlayerLauncher);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            // Latched rather than gated on respawningAfterLoad, so a javelin that was already
            // standing on a map in a save from before this feature existed is handed a warhead the
            // first time it comes back rather than being stuck on basic rockets for good. Once the
            // flag is set it is never rolled again, including across reloads.
            if (loadoutAssigned)
            {
                return;
            }

            loadoutAssigned = true;

            // The player picks their own. Everyone else is handed one, because there is nothing on
            // a raid that would ever operate the gizmo.
            if (IsPlayerLauncher)
            {
                return;
            }

            List<JavelinRocketOption> rockets = Rockets;
            if (rockets == null || rockets.Count == 0)
            {
                return;
            }

            bool[] playerOnly = new bool[rockets.Count];
            for (int i = 0; i < rockets.Count; i++)
            {
                playerOnly[i] = rockets[i].playerOnly;
            }

            loadedKey = rockets[JavelinRocketSelection.RandomEnemyRocket(playerOnly, Rand.Value)].key;
        }

        /// <summary>
        /// Called by <see cref="Verb_ShootJavelin"/> once a missile has actually left the tube.
        /// </summary>
        public void Notify_ShotFired()
        {
            JavelinRocketOption fired = Firing;
            if (fired != null && JavelinRocketSelection.ChargesFor(fired.uraniumCost, IsPlayerLauncher))
            {
                Magazine?.ConsumeFuel(fired.uraniumCost);
            }

            // Checked whatever just left the tube, including the shot that was already a fallback:
            // otherwise a rack switched to a warhead it cannot afford would sit there firing basic
            // rockets while the gizmo went on claiming it was loaded with something else.
            JavelinRocketOption loaded = Loaded;
            if (loaded == null || loaded == Fallback || CanFire(loaded))
            {
                return;
            }

            loadedKey = Fallback?.key;
            if (IsPlayerLauncher)
            {
                Messages.Message(
                    "APM.Javelin.OutOfUranium".Translate(parent.LabelShortCap, loaded.label),
                    parent,
                    MessageTypeDefOf.CautionInput,
                    historical: false);
            }
        }

        public override string CompInspectStringExtra()
        {
            JavelinRocketOption loaded = Loaded;
            if (loaded == null)
            {
                return null;
            }

            string line = "APM.Javelin.LoadedRocket".Translate(loaded.label);

            // The fallback is worth calling out on the inspect pane rather than only in the message
            // that fired once, because from outside it looks exactly like the rocket the player asked
            // for and lands for a fraction of the damage.
            if (!CanFire(loaded))
            {
                line += " " + "APM.Javelin.NoUranium".Translate();
            }

            return line;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            List<JavelinRocketOption> rockets = Rockets;
            if (!IsPlayerLauncher || rockets == null || rockets.Count < 2)
            {
                yield break;
            }

            JavelinRocketOption loaded = Loaded;
            yield return new Command_Action
            {
                defaultLabel = "APM.Javelin.SwitchRocket.Label".Translate(loaded?.label ?? string.Empty),
                defaultDesc = "APM.Javelin.SwitchRocket.Desc".Translate(),
                icon = RocketIcon(loaded),
                defaultIconColor = RocketColor(loaded),
                action = OpenRocketMenu
            };
        }

        private void OpenRocketMenu()
        {
            List<JavelinRocketOption> rockets = Rockets;
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            for (int i = 0; i < rockets.Count; i++)
            {
                JavelinRocketOption rocket = rockets[i];
                if (!CanSelect(rocket))
                {
                    continue;
                }

                string label = rocket.uraniumCost > 0
                    ? "APM.Javelin.RocketCost".Translate(rocket.label, rocket.uraniumCost)
                    : "APM.Javelin.RocketFree".Translate(rocket.label);

                // Unaffordable warheads are shown rather than hidden, so the player can see what the
                // rack could be loaded with and what it would take to get there.
                bool affordable = CanFire(rocket);
                if (!affordable)
                {
                    label += " " + "APM.Javelin.NoUranium".Translate();
                }

                JavelinRocketOption picked = rocket;
                FloatMenuOption option = new FloatMenuOption(label, () => loadedKey = picked.key)
                {
                    Disabled = !affordable,
                    tooltip = rocket.description.NullOrEmpty() ? (TipSignal?)null : new TipSignal(rocket.description)
                };

                options.Add(option);
            }

            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private static Texture2D RocketIcon(JavelinRocketOption rocket)
        {
            string path = rocket?.projectile?.graphicData?.texPath;
            return path.NullOrEmpty() ? BaseContent.BadTex : ContentFinder<Texture2D>.Get(path, false) ?? BaseContent.BadTex;
        }

        private static Color RocketColor(JavelinRocketOption rocket)
        {
            return rocket?.projectile?.graphicData?.color ?? Color.white;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref loadedKey, nameof(loadedKey));
            Scribe_Values.Look(ref loadoutAssigned, nameof(loadoutAssigned));
        }
    }
}
