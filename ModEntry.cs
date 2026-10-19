using System;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace BulkMachines
{
    public class ModEntry : Mod
    {
        internal static IMonitor ModMonitor;
        internal const string ModDataKey = "SeuNome.BulkAmount";

        public override void Entry(IModHelper helper)
        {
            ModMonitor = this.Monitor;
            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll();
        }
    }

    // --- PATCH 1: Colocação do Item ---
    [HarmonyPatch]
    public static class MachineInputPatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(StardewValley.Object))
                .FirstOrDefault(m => m.Name == "performObjectDropInAction" && m.GetParameters().Length >= 3);
        }

        // Prefix roda ANTES do jogo consumir o item. Contamos a quantidade total aqui.
        static void Prefix(StardewValley.Object __instance, Item dropInItem, bool probe, Farmer who, ref int __state)
        {
            if (probe || who == null || dropInItem == null) return;
            
            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)24")
            {
                // Salva a quantidade total no inventário (ex: 1, 2, 5, 10...)
                __state = who.Items.CountId(dropInItem.QualifiedItemId);
            }
        }

        static void Postfix(StardewValley.Object __instance, Item dropInItem, bool probe, Farmer who, ref bool __result, int __state)
        {
            if (probe || !__result || who == null || dropInItem == null) return;

            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)24")
            {
                if (__instance.heldObject.Value != null && !__instance.readyForHarvest.Value)
                {
                    __instance.modData.Remove(ModEntry.ModDataKey);

                    // O jogo já consumiu 1 item. Queremos processar até 5.
                    int totalToProcess = Math.Min(5, __state);
                    int extraToConsume = totalToProcess - 1; // O jogo já consumiu 1.

                    if (extraToConsume > 0)
                    {
                        who.Items.ReduceId(dropInItem.QualifiedItemId, extraToConsume);
                        __instance.modData[ModEntry.ModDataKey] = totalToProcess.ToString();
                    }
                    else
                    {
                        __instance.modData[ModEntry.ModDataKey] = "1";
                    }
                }
            }
        }
    }

    // --- PATCH 2: Modifica a saída na hora da colheita ---
    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.checkForAction))]
    public static class MachineOutputPatch
    {
        static void Prefix(StardewValley.Object __instance, Farmer who, bool justCheckingForActivity)
        {
            if (justCheckingForActivity || !__instance.readyForHarvest.Value || __instance.heldObject.Value == null) return;

            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)24")
            {
                if (__instance.modData.TryGetValue(ModEntry.ModDataKey, out string amountStr) && int.TryParse(amountStr, out int amount))
                {
                    __instance.heldObject.Value.Stack = amount;
                }
            }
        }

        static void Postfix(StardewValley.Object __instance, Farmer who, bool justCheckingForActivity)
        {
            if (!justCheckingForActivity && __instance.heldObject.Value == null)
            {
                __instance.modData.Remove(ModEntry.ModDataKey);
            }
        }
    }

    // --- PATCH 3 e 4: Limpezas de segurança ---
    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.performRemoveAction))]
    public static class MachineRemovePatch
    {
        static void Postfix(StardewValley.Object __instance)
        {
            __instance.modData.Remove(ModEntry.ModDataKey);
        }
    }

    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.DayUpdate))]
    public static class MachineDayUpdatePatch
    {
        static void Postfix(StardewValley.Object __instance)
        {
            if (__instance.heldObject.Value == null)
            {
                __instance.modData.Remove(ModEntry.ModDataKey);
            }
        }
    }
}
