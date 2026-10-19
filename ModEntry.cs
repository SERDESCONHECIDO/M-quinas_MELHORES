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

    // --- PATCH 1: Conta a quantidade na pilha ANTES de o jogo consumir ---
    [HarmonyPatch]
    public static class MachineInputPatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(StardewValley.Object))
                .FirstOrDefault(m => m.Name == "performObjectDropInAction" && m.GetParameters().Length >= 3);
        }

        static void Prefix(StardewValley.Object __instance, Item dropInItem, bool probe, Farmer who, out int __state)
        {
            __state = 0;
            if (probe || who == null || dropInItem == null) return;

            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)24")
            {
                // Salva a quantidade EXATA da pilha que está sendo colocada
                __state = dropInItem.Stack;
            }
        }

        static void Postfix(StardewValley.Object __instance, Item dropInItem, bool probe, Farmer who, ref bool __result, int __state)
        {
            if (probe || !__result || who == null || dropInItem == null) return;

            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)24")
            {
                if (__instance.heldObject.Value != null && !__instance.readyForHarvest.Value)
                {
                    // O jogo consumiu 1. Vamos consumir os extras até o limite de 5.
                    int totalToProcess = Math.Min(5, Math.Max(1, __state));
                    int extraToConsume = totalToProcess - 1;

                    if (extraToConsume > 0)
                    {
                        who.Items.ReduceId(dropInItem.QualifiedItemId, extraToConsume);
                    }

                    // Salva a quantidade total a ser entregue (1 do jogo + extras)
                    __instance.modData["BulkAmount"] = totalToProcess.ToString();
                }
            }
        }
    }

    // --- PATCH 2: Entrega os itens extras DEPOIS que o jogo deu o item original ---
    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.checkForAction))]
    public static class HarvestPatch
    {
        static void Postfix(StardewValley.Object __instance, Farmer who, bool justCheckingForActivity)
        {
            if (justCheckingForActivity || who == null) return;
            if (__instance.heldObject.Value != null || __instance.readyForHarvest.Value) return;

            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)24")
            {
                if (__instance.modData.TryGetValue("BulkAmount", out string amountStr) && int.TryParse(amountStr, out int amount))
                {
                    __instance.modData.Remove("BulkAmount");

                    int extras = amount - 1;
                    if (extras > 0)
                    {
                        // (O)424 = Queijo | (O)306 = Maionese
                        string outputId = __instance.QualifiedItemId == "(BC)16" ? "(O)424" : "(O)306";
                        for (int i = 0; i < extras; i++)
                        {
                            who.addItemToInventory(ItemRegistry.Create(outputId));
                        }
                    }
                }
            }
        }
    }

    // --- PATCH 3: Limpeza ao quebrar a máquina ---
    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.performRemoveAction))]
    public static class MachineRemovePatch
    {
        static void Postfix(StardewValley.Object __instance)
        {
            __instance.modData.Remove("BulkAmount");
        }
    }
}
