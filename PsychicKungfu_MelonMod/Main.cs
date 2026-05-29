using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using PsychicKungfu_MelonMod;
using PsychicKungfu_MelonMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PsychicKungfu_MelonMod
{
    [BepInPlugin(MyGUID, PluginName, VersionString)]
    public class Main : BaseUnityPlugin
    {
        private const string MyGUID = "com.Jingu.MiscMods";
        private const string PluginName = "Jingu_MiscMods";
        private const string VersionString = "1.0.0";
        public static readonly string pluginDir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly Harmony Harmony = new Harmony(MyGUID);
        public static ManualLogSource Log = new ManualLogSource(PluginName);

        private void Awake()
        {
            Log = Logger;
            ModConfig.Initialize(Config);
            SkillReplacer.Initialize(Config);
            if (ModConfig.DumpLanguage.Value != -1)
            {
                DBLoadManager.DumpAll((DumpLanguage)ModConfig.DumpLanguage.Value);
            }           
            DBLoadManager.LoadOverrides();
            Harmony.PatchAll();
            AnimationMerger.Initialize();         
            Logger.LogInfo($"PluginName: {PluginName}, VersionString: {VersionString} is loaded.");         

        }

        private void Update()
        {
            SkillReplacer.Update();
        }


    }
}
