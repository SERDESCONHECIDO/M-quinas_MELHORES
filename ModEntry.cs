using System;
using System.Linq; // CORREÇÃO 1: Adicionado para o FirstOrDefault funcionar
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

        // Função manual para consumir itens (substitui o removeItemsFromInventory)
        internal static void ConsumeItems(Farmer who, string itemId, int count)
        {
            if (who == null) return;

            int remaining = count;
            for (int i = 0; i < who.Items.Count && remaining > 0; i++)
            {
                var item = who.Items[i];
                if (item != null && item.QualifiedItemId == itemId)
                {
                    int take = Math.Min(item.Stack, remaining);
                    item.Stack -= take;
                    remaining -= take;
                    if (item.Stack <= 0) who.Items[i] = null;
                }
            }
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

        static void Postfix(StardewValley.Object __instance, Item dropInItem, bool probe, Farmer who, ref bool __result)
        {
            if (probe || !__result || who == null || dropInItem == null) return;

            // (BC)16 = Prensa de Queijo | (BC)19 = Máquina de Maionese
            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)19")
            {
                if (__instance.heldObject.Value != null && !__instance.readyForHarvest.Value)
                {
                    __instance.modData.Remove(ModEntry.ModDataKey);

                    int extraAvailable = 0;
                    foreach (var item in who.Items)
                    {
                        if (item != null && item.QualifiedItemId == dropInItem.QualifiedItemId)
                        {
                            extraAvailable += item.Stack;
                        }
                    }

                    int toConsume = Math.Min(4, extraAvailable);

                    if (toConsume > 0)
                    {
                        // CORREÇÃO 2: Usa a função manual ConsumeItems em vez do método inexistente
                        ModEntry.ConsumeItems(who, dropInItem.QualifiedItemId, toConsume);
                        __instance.modData[ModEntry.ModDataKey] = (1 + toConsume).ToString();
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

            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)19")
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

    // --- PATCH 3: Limpeza ao quebrar a máquina ---
    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.performRemoveAction))]
    public static class MachineRemovePatch
    {
        static void Postfix(StardewValley.Object __instance)
        {
            if (__instance.modData.ContainsKey(ModEntry.ModDataKey))
            {
                __instance.modData.Remove(ModEntry.ModDataKey);
            }
        }
    }

    // --- PATCH 4: Limpeza diária (Failsafe contra o Automate) ---
    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.DayUpdate))]
    public static class MachineDayUpdatePatch
    {
        static void Postfix(StardewValley.Object __instance)
        {
            if (__instance.heldObject.Value == null && __instance.modData.ContainsKey(ModEntry.ModDataKey))
            {
                __instance.modData.Remove(ModEntry.ModDataKey);
            }
        }
    }
}
