using DBLoad;
using HarmonyLib;

namespace PsychicKungfu_MelonMod.Patches
{
    [HarmonyPatch]
    internal class SaveData_Patches
    {
        [HarmonyPatch(typeof(SaveData), "get_FullName")]
        [HarmonyPostfix]
        public static void ChangePlayerName(SaveData __instance, ref string __result)
        {
            //MelonLogger.Msg($"Successfully changed player name!");
            __instance.m_leaderFamily = "Igris";
            __instance.m_leaderName = "";
            __result = "Igris";
        }

        [HarmonyPatch(typeof(SaveData), "AddEffectValue")]
        [HarmonyPrefix]
        public static void ValueBoost(ref float add, EffectId id)
        {
            int effect = (int)id;
            if (effect < 0x12D || effect > 0x134) return;
            add *= 2;
        }

        [HarmonyPatch(typeof(SaveData), "ReadWuXue")]
        [HarmonyPostfix]
        public static void MaxWuxueOnRead(SaveData __instance, bool __result, int id)
        {
            if (!__result) return;
            WuXueData data = WuXue.Get(id);
            if (data == null) return;
            //__instance.AddWuXueExp(id, data.m_exp * 20, true);
        }
    }
}