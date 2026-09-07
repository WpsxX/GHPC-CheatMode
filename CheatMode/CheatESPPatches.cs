using GHPC;
using GHPC.Infantry;
using HarmonyLib;

namespace CheatMode
{
    internal class CheatESPPatches
    {
        [HarmonyPatch(typeof(Unit), "Start")]
        internal static class Unit_Start
        {
            private static void Postfix(Unit __instance)
            {
                if (CheatModeMod.Instance == null)
                {
                    return;
                }
                if (!CheatModeMod.Instance.Units.Contains(__instance))
                {
                    CheatModeMod.Instance.Units.Add(__instance);
                }
            }
        }

        [HarmonyPatch(typeof(InfantryUnit), "Start")]
        internal static class InfantryUnit_Start
        {
            private static void Postfix(Unit __instance)
            {
                if (CheatModeMod.Instance == null)
                {
                    return;
                }
                if (!CheatModeMod.Instance.InfantryUnits.Contains(__instance))
                {
                    CheatModeMod.Instance.InfantryUnits.Add(__instance);
                }
            }
        }
    }
}
