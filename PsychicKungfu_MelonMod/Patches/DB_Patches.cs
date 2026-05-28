using DBLoad;
using HarmonyLib;

namespace PsychicKungfu_MelonMod.Patches
{
    [HarmonyPatch]
    internal class DB_Patches
    {
        [HarmonyPatch(typeof(WuXue), nameof(WuXue.Get))]
        [HarmonyPostfix]
        public static void GetWuxue(ref WuXueData __result)
        {
            //if ((WuXueType)__result.m_type == WuXueType.All || __result.m_type > 6) return;
            WuXueData data = null;
            if ((WuXueType)__result.m_type != WuXueType.All && __result.m_type <= 6)
            {
                data = new WuXueData(
                __result.m_id,
                (int)WuXueType.All,
                __result.m_lvMax,
                __result.m_exp,
                __result.m_lvNeed,
                __result.m_skill,
                __result.m_effectId,
                __result.m_effectValue,
                new int[] { },
                new int[][] { },
                __result.m_desc,
                __result.m_special
                );
            }
            else if (__result.m_type > 6)
            {
                data = new WuXueData(
                __result.m_id,
                __result.m_type,
                __result.m_lvMax,
                __result.m_exp,
                __result.m_lvNeed,
                __result.m_skill,
                __result.m_effectId,
                __result.m_effectValue,
                new int[] { },
                new int[][] { },
                __result.m_desc,
                __result.m_special
                );
            }
            if (data == null) return;
            __result = data;

        }
    }
}