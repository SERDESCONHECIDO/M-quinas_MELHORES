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

    // --- PATCH ÚNICO: Conta o inventário e altera a saída na hora da colocação ---
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
            // Se for apenas teste, falhou, ou não tem jogador, ignora.
            if (probe || !__result || who == null || dropInItem == null) return;

            // (BC)16 = Prensa de Queijo | (BC)24 = Máquina de Maionese
            if (__instance.QualifiedItemId == "(BC)16" || __instance.QualifiedItemId == "(BC)24")
            {
                // Se a máquina ligou e começou a processar
                if (__instance.heldObject.Value != null && !__instance.readyForHarvest.Value)
                {
                    // O jogo já consumiu 1 item. Vamos ver quantos sobraram no inventário AGORA.
                    int currentInventory = who.Items.CountId(dropInItem.QualifiedItemId);
                    
                    // Pega até 4 itens extras (para totalizar 5)
                    int extraToConsume = Math.Min(4, Math.Max(0, currentInventory));
                    int totalToProcess = 1 + extraToConsume;

                    if (extraToConsume > 0)
                    {
                        // Remove os itens extras do inventário
                        who.Items.ReduceId(dropInItem.QualifiedItemId, extraToConsume);
                    }
                    
                    // A MÁGICA: Altera a quantidade do item de saída DIRETAMENTE na máquina.
                    // Quando a máquina terminar, ela vai cuspir essa quantidade exata.
                    __instance.heldObject.Value.Stack = totalToProcess;
                }
            }
        }
    }
}
