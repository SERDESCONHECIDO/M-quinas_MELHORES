using System;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace BulkMachines
{
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll();
        }
    }

    [HarmonyPatch]
    public static class MachineInputPatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(StardewValley.Object))
                .FirstOrDefault(m => m.Name == "performObjectDropInAction" && m.GetParameters().Length >= 3);
        }

        static void Postfix(StardewValley.Object __instance, Item dropInItem, bool probe, Farmer who, ref bool __result)
        {
            if (probe || !__result || who == null || dropInItem == null) return;

            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)24")
            {
                if (__instance.heldObject.Value != null && !__instance.readyForHarvest.Value)
                {
                    __instance.modData.Remove("BulkAmount");

                    int available = dropInItem.Stack;
                    int toConsume = Math.Min(4, Math.Max(0, available - 1));

                    if (toConsume > 0)
                    {
                        who.Items.ReduceId(dropInItem.QualifiedItemId, toConsume);
                    }

                    int totalOutput = 1 + toConsume;
                    __instance.modData["BulkAmount"] = totalOutput.ToString();
                }
            }
        }
    }

    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.checkForAction))]
    public static class HarvestPatch
    {
        static bool Prefix(StardewValley.Object __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
        {
            if (justCheckingForActivity || !__instance.readyForHarvest.Value || __instance.heldObject.Value == null) return true;

            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)24")
            {
                int amount = 1;
                if (__instance.modData.TryGetValue("BulkAmount", out string amountStr))
                {
                    int.TryParse(amountStr, out amount);
                    __instance.modData.Remove("BulkAmount");
                }

                for (int i = 0; i < amount; i++)
                {
                    if (who != null) who.addItemToInventory(__instance.heldObject.Value.getOne());
                }

                __instance.heldObject.Value = null;
                __instance.readyForHarvest.Value = false;
                __instance.minutesUntilReady.Value = 0;

                if (who != null) who.currentLocation.playSound("coin");

                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.performRemoveAction))]
    public static class MachineRemovePatch
    {
        static void Postfix(StardewValley.Object __instance)
        {
            __instance.modData.Remove("BulkAmount");
        }
    }
}
