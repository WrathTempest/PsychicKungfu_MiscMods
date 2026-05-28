using DBLoad;
using HarmonyLib;
using MelonLoader;
using PsychicKungfu_MelonMod.Utils;
using System;
using System.Reflection;
using UnityEngine;

namespace PsychicKungfu_MelonMod.Patches
{
    [HarmonyPatch]
    internal class Crafting_Patches
    {
        [HarmonyPatch(typeof(DaZaoWindow), "SetInfo")]
        [HarmonyPostfix]
        public static void DaZaoBypassItem(DaZaoWindow __instance)
        {
            if (!Helpers.PlayerHasGlobalBuff(6942)) return;
            //Helpers.SetPrivateField<bool>(__instance, "m_itemEnough", true);
            DaZaoData m_selectDaZao = Helpers.GetPrivateField<DaZaoData>(__instance, "m_selectDaZao");
            if (m_selectDaZao == null) 
            {
                return;
            }
            SaveData saveData = MonoSingleton<SaveManager>.Instance.SaveData;
            for (int i = 0; i < m_selectDaZao.m_item.Length; i++)
            {
                int num = m_selectDaZao.m_item[i];
                int num2 = m_selectDaZao.m_num[i];
                if (saveData.GetItemNum(num) < num2)
                {
                    MelonLogger.Msg($"Adding insufficient items... Item ID: {num}, Item Amount: {num2}");
                    saveData.ChangeItem(num, num2*20, false);
                }
                
            }

            foreach (var nested in typeof(LianDanReadyWindow).GetNestedTypes(
    BindingFlags.NonPublic |
    BindingFlags.Public))
            {
                MelonLogger.Msg($"NESTED: {nested.FullName}");

                foreach (var m in nested.GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic))
                {
                    MelonLogger.Msg($"    {m.Name}");
                }
            }
        }

        [HarmonyPatch]
        public static class LianDanBypassItemPatch
        {
            static MethodBase TargetMethod()
            {
                Type nestedType = typeof(LianDanReadyWindow)
                    .GetNestedType(
                        "<>c__DisplayClass7_0",
                        BindingFlags.NonPublic);

                if (nestedType == null)
                {
                    MelonLogger.Msg("Failed to find nested type");
                    return null;
                }

                MethodInfo method = nestedType.GetMethod(
                    "<OnOpen>g__SetInfo|3",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public);

                if (method == null)
                {
                    MelonLogger.Msg("Failed to find target method");
                    return null;
                }

                MelonLogger.Msg(
                    $"Patching: {nestedType.FullName}.{method.Name}");

                return method;
            }

            [HarmonyPostfix]
            public static void Postfix(object __instance)
            {
                try
                {
                    FieldInfo windowField =
                        __instance.GetType().GetField(
                            "<>4__this",
                            BindingFlags.Instance |
                            BindingFlags.NonPublic |
                            BindingFlags.Public);

                    if (windowField == null)
                    {
                        MelonLogger.Msg("Failed to get <>4__this");
                        return;
                    }

                    LianDanReadyWindow window =
                        (LianDanReadyWindow)windowField.GetValue(__instance);

                    if (window == null)
                    {
                        MelonLogger.Msg("Window was null");
                        return;
                    }

                    if (!Helpers.PlayerHasGlobalBuff(6942))
                        return;

                    SaveData saveData =
                        MonoSingleton<SaveManager>.Instance.SaveData;

                    int m_selectDanFang =
                        Helpers.GetPrivateField<int>(
                            window,
                            "m_selectDanFang");

                    DanFangData danFangData =
                        DanFang.Get(m_selectDanFang);

                    for (int i = 0; i < danFangData.m_item.Length; i++)
                    {
                        int num = danFangData.m_item[i];
                        int num2 = danFangData.m_itemNum[i];

                        if (saveData.GetItemNum(num) < num2)
                        {
                            MelonLogger.Msg(
                                $"Adding insufficient items... " +
                                $"Item ID: {num}, " +
                                $"Item Amount: {num2}");

                            saveData.ChangeItem(
                                num,
                                num2 * 20,
                                false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error(ex.ToString());
                }
            }
        }
    }
}