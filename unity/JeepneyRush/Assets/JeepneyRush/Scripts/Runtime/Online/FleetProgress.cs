using System;
using JeepneyRush.Logic;

namespace JeepneyRush
{
    /// <summary>Operator mode for the UI: the server when online, else the save file (same rules as Logic/Operator).</summary>
    public static class FleetProgress
    {
        private static bool Online => Services.Online != null && Services.Online.Configured;
        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static void Status(Action<FleetInfo, string> done)
        {
            if (Online) { if (Services.Online.SignedIn) Services.Online.FleetStatus(done); else done(null, "Sign in under ACCOUNT first."); return; }
            var d = Services.Save.Data;
            int slots = Operator.Slots(d.careerRank, d.fleetBought);
            var list = new System.Collections.Generic.List<FleetSlot>();
            for (int i = 0; i < d.fleetJeepneys.Count && i < slots; i++)
            {
                string j = d.fleetJeepneys[i];
                if (string.IsNullOrEmpty(j)) continue;
                int rate = Operator.RatePerHour(JeepneyCatalog.Get(j));
                long since = i < d.fleetSince.Count ? d.fleetSince[i] : Now;
                list.Add(new FleetSlot { slot = i, jeepney = j, rate = rate, pending = Operator.Pending(rate, since, Now), hours = Math.Min(Operator.CapHours, (Now - since) / 3600f) });
            }
            done(new FleetInfo { unlocked = Operator.Unlocked(d.careerRank), slots = slots, bought = d.fleetBought, next_slot_price = Operator.NextSlotPrice(d.fleetBought), fleet = list.ToArray() }, null);
        }

        private static void Ensure(SaveData d, int slot)
        {
            while (d.fleetJeepneys.Count <= slot) d.fleetJeepneys.Add("");
            while (d.fleetSince.Count <= slot) d.fleetSince.Add(Now);
        }

        private static int PayLocal(SaveData d, int slot)
        {
            int paid = 0;
            for (int i = 0; i < d.fleetJeepneys.Count; i++)
            {
                if (slot >= 0 && i != slot) continue;
                Ensure(d, i);
                if (!string.IsNullOrEmpty(d.fleetJeepneys[i])) paid += Operator.Pending(Operator.RatePerHour(JeepneyCatalog.Get(d.fleetJeepneys[i])), d.fleetSince[i], Now);
                d.fleetSince[i] = Now;
            }
            d.coins += paid;
            return paid;
        }

        public static void Assign(int slot, string jeepney, Action<bool, string, int> done)
        {
            if (Online) { Services.Online.FleetAssign(slot, jeepney, done); return; }
            var d = Services.Save.Data;
            if (slot < 0 || slot >= Operator.Slots(d.careerRank, d.fleetBought)) { done(false, "Buy another slot first.", 0); return; }
            if (!d.Owns(jeepney)) { done(false, "You don't own that jeepney.", 0); return; }
            for (int i = 0; i < d.fleetJeepneys.Count; i++) if (i != slot && d.fleetJeepneys[i] == jeepney) { done(false, "That jeepney is already driving another route.", 0); return; }
            Ensure(d, slot);
            int paid = PayLocal(d, slot);
            d.fleetJeepneys[slot] = jeepney;
            d.fleetSince[slot] = Now;
            Services.Save.Save();
            done(true, null, paid);
        }

        public static void Unassign(int slot, Action<bool, string, int> done)
        {
            if (Online) { Services.Online.FleetUnassign(slot, done); return; }
            var d = Services.Save.Data;
            Ensure(d, slot);
            int paid = PayLocal(d, slot);
            d.fleetJeepneys[slot] = "";
            Services.Save.Save();
            done(true, null, paid);
        }

        public static void Collect(Action<bool, string, int> done)
        {
            if (Online) { Services.Online.FleetCollect(done); return; }
            var d = Services.Save.Data;
            int paid = PayLocal(d, -1);
            Services.Save.Save();
            done(paid > 0, paid > 0 ? null : "Wala pang boundary – come back later.", paid);
        }

        public static void BuySlot(Action<bool, string, int> done)
        {
            if (Online) { Services.Online.FleetBuySlot(done); return; }
            var d = Services.Save.Data;
            int price = Operator.NextSlotPrice(d.fleetBought);
            if (!Operator.Unlocked(d.careerRank)) { done(false, "Get the OPERATOR license in career first.", 0); return; }
            if (price <= 0) { done(false, "Max level na.", 0); return; }
            if (d.coins < price) { done(false, "Kulang ang coins.", 0); return; }
            d.coins -= price;
            d.fleetBought++;
            Services.Save.Save();
            done(true, null, 0);
        }
    }
}
