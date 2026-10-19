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

    // --- PATCH 1: Conta o item na mão e altera a quantidade ---
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

            // (BC)16 = Prensa de Queijo | (BC)24 = Máquina de Maionese
            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)24")
            {
                if (__instance.heldObject.Value != null && !__instance.readyForHarvest.Value)
                {
                    // Limpa a memória antiga da máquina imediatamente
                    __instance.modData.Remove("BulkAmount");

                    // Pega a quantidade exata da PILHA que está sendo colocada
                    int available = dropInItem.Stack;
                    
                    // O jogo vai consumir 1 automaticamente. Nós consumimos os extras (até 4).
                    int toConsume = Math.Min(4, Math.Max(0, available - 1));
                    
                    if (toConsume > 0)
                    {
                        who.Items.ReduceId(dropInItem.QualifiedItemId, toConsume);
                    }
                    
                    // Define a saída total (1 do jogo + extras consumidos)
                    int totalOutput = 1 + toConsume;
                    __instance.modData["BulkAmount"] = totalOutput.ToString();
                }
            }
        }
    }

    // --- PATCH 2: Substitui a colheita do jogo ---
    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.checkForAction))]
    public static class HarvestPatch
    {
        static bool Prefix(StardewValley.Object __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
        {
            if (justCheckingForActivity || !__instance.readyForHarvest.Value || __instance.heldObject.Value == null) return true;

            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)24")
            {
                // Pega a quantidade salva (padrão é 1)
                int amount = 1;
                if (__instance.modData.TryGetValue("BulkAmount", out string amountStr))
                {
                    int.TryParse(amountStr, out amount);
                    __instance.modData.Remove("BulkAmount"); // Limpa a memória após a colheita
                }

                // Entrega os itens e limpa a máquina
                for (int i = 0; i < amount; i++)
                {
                    if (who != null) who.addItemToInventory(__instance.heldObject.Value.getOne());
                }

                __instance.heldObject.Value = null;
                __instance.readyForHarvest.Value = false;
                __instance.minutesUntilReady.Value = 0;

                if (who != null) who.currentLocation.playSound("coin");

                __result = true;
                return false; // Impede o jogo de dar o item original (1 unidade)
            }
            return true;
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
                    // Pega o mínimo entre 5 e a quantidade total que o jogador tinha
                    int totalToProcess = Math.Min(5, Math.Max(1, __state));
                    
                    // O jogo já consumiu 1, então removemos os extras (total - 1)
                    int extraToConsume = totalToProcess - 1;
                    
                    if (extraToConsume > 0)
                    {
                        who.Items.ReduceId(dropInItem.QualifiedItemId, extraToConsume);
                    }
                    
                    // Salva a quantidade exata que deve ser produzida
                    __instance.modData["BulkAmount"] = totalToProcess.ToString();
                }
            }
        }
    }

    // --- PATCH 2: Substitui a colheita do jogo para entregar a quantidade exata ---
    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.checkForAction))]
    public static class HarvestPatch
    {
        static bool Prefix(StardewValley.Object __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
        {
            if (justCheckingForActivity || !__instance.readyForHarvest.Value || __instance.heldObject.Value == null) return true;

            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)24")
            {
                // Pega a quantidade que foi salva na máquina
                int amount = 1;
                if (__instance.modData.TryGetValue("BulkAmount", out string amountStr))
                {
                    int.TryParse(amountStr, out amount);
                }

                // Entrega a quantidade exata de itens
                for (int i = 0; i < amount; i++)
                {
                    if (who != null) who.addItemToInventory(__instance.heldObject.Value.getOne());
                }

                // Limpa a máquina
                __instance.heldObject.Value = null;
                __instance.readyForHarvest.Value = false;
                __instance.minutesUntilReady.Value = 0;
                __instance.modData.Remove("BulkAmount");

                if (who != null) who.currentLocation.playSound("coin");

                __result = true;
                return false; // Cancela a colheita original do jogo
            }
            return true;
        }
    }

    // --- PATCH 3: Limpeza de segurança ---
    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.performRemoveAction))]
    public static class MachineRemovePatch
    {
        static void Postfix(StardewValley.Object __instance)
        {
            __instance.modData.Remove("BulkAmount");
        }
    }
}
