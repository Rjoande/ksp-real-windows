// Crew-portrait bookkeeping for interiors this mod spawns on NON-active
// vessels: spawning crew there would register their 2D portraits in the
// active vessel's portrait gallery.

using System;
using System.Collections.Generic;
using KSP.UI.Screens.Flight;

namespace RealWindows
{
    public static class RWPortraits
    {
        private static bool HasPortrait(Kerbal crew)
        {
            for (int i = 0; i < KerbalPortraitGallery.Instance.Portraits.Count; ++i)
            {
                if (KerbalPortraitGallery.Instance.Portraits[i].crewMember == crew)
                    return true;
            }
            return false;
        }

        private static bool InActiveCrew(Kerbal crew)
        {
            // identity, not name: two same-named kerbals must not count for each other
            var items = KerbalPortraitGallery.Instance.ActiveCrewItems;
            for (int i = 0; i < items.Count; ++i)
            {
                if (items[i].kerbal == crew)
                    return true;
            }
            return false;
        }

        private static void DestroyPortrait(Kerbal kerbal)
        {
            kerbal.InPart = null;
            kerbal.SetVisibleInPortrait(false);
            kerbal.state = Kerbal.States.NO_SIGNAL;
            // Unregister from the gallery's live active-crew list, by identity.
            // Not the obsolete ActiveCrew property: its getter returns a fresh
            // copy on every access, so removing from it did nothing, the stale
            // entry survived, and SetActivePortraitsForVessel re-spawned the
            // portrait on the next gallery refresh (vessel switch, vessel
            // modify such as a decoupling, camera change).
            var items = KerbalPortraitGallery.Instance.ActiveCrewItems;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i].kerbal == kerbal)
                    items.RemoveAt(i);
            }
            KerbalPortraitGallery.Instance.DespawnInactivePortraits();
            KerbalPortraitGallery.Instance.DespawnPortrait(kerbal);
            KerbalPortraitGallery.Instance.UIControlsUpdate();
        }

        private static void RestorePortrait(Part part, Kerbal kerbal)
        {
            if (kerbal.rosterStatus == ProtoCrewMember.RosterStatus.Dead ||
                kerbal.protoCrewMember.type == ProtoCrewMember.KerbalType.Unowned)
                return;
            kerbal.InPart = part;
            kerbal.SetVisibleInPortrait(true);
            if (!InActiveCrew(kerbal) && !HasPortrait(kerbal))
            {
                kerbal.staticOverlayDuration = 1f;
                kerbal.randomizeOnStartup = false;
                kerbal.Start();
            }
            kerbal.state = Kerbal.States.ALIVE;
        }

        public static void Process(Part part)
        {
            try
            {
                if (part.internalModel == null || KerbalPortraitGallery.Instance == null)
                    return;
                List<InternalSeat> seats = part.internalModel.seats;
                for (int i = 0; i < seats.Count; i++)
                {
                    Kerbal k = seats[i].kerbalRef;
                    if (k == null)
                        continue;
                    if (part.vessel.isActiveVessel)
                    {
                        if (k.InPart != part || !HasPortrait(k))
                            RestorePortrait(part, k);
                    }
                    // also a kerbal still in the gallery's live list with no portrait:
                    // the next gallery refresh would spawn a static-noise one for it
                    else if (k.InPart != null || HasPortrait(k) || InActiveCrew(k))
                    {
                        DestroyPortrait(k);
                    }
                }
            }
            catch (Exception e)
            {
                RWLog.Verbose("portrait processing failed for {0}: {1}", part.name, e.Message);
            }
        }
    }
}
