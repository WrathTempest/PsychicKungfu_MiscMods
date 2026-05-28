using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace PsychicKungfu_MelonMod
{
    /// <summary>
    /// Matches the game's SettingEnum.Language values:
    ///   0 = Simplified Chinese (raw, no lookup)
    ///   1 = Traditional Chinese
    ///   2 = English
    /// </summary>
    public enum DumpLanguage
    {
        SimplifiedChinese = 0,
        TraditionalChinese = 1,
        English = 2
    }

    public static class DBLoadManager
    {
        private const string DumpFolderName = "DBDump";
        private const string OverrideFolderName = "DBOverride";

        private static string PluginDir =>
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private static string DumpDir => Path.Combine(PluginDir, DumpFolderName);
        private static string OverrideDir => Path.Combine(PluginDir, OverrideFolderName);

        private static readonly JsonSerializerSettings SerializeSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new PublicFieldsContractResolver()
        };

        // ── Localization reflection cache ────────────────────────────────────
        private static bool _locInitialized;
        private static MethodInfo _languageGetMethod;   // Language.Get(int) → LanguageData
        private static FieldInfo _traditionalField;    // LanguageData.m_Traditional
        private static FieldInfo _englishField;        // LanguageData.m_English

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Dumps every DBLoad dictionary to JSON.
        /// Pass a DumpLanguage value to replace Chinese strings with the
        /// localized version where a translation exists.
        /// </summary>
        public static void DumpAll(DumpLanguage language = DumpLanguage.SimplifiedChinese)
        {
            Directory.CreateDirectory(DumpDir);

            if (language != DumpLanguage.SimplifiedChinese)
                EnsureLocalizationReady();

            foreach (Type dbType in GetAllDBTypes())
            {
                try { DumpType(dbType, language); }
                catch (Exception e)
                { MelonLogger.Error($"[DBLoad] Dump failed for {dbType.Name}: {e}"); }
            }
        }

        /// <summary>
        /// Reads every JSON in the override folder and replaces / appends
        /// entries in the corresponding live dictionary.
        /// </summary>
        public static void LoadOverrides()
        {
            if (!Directory.Exists(OverrideDir))
            {
                Directory.CreateDirectory(OverrideDir);
                MelonLogger.Msg($"[DBLoad] Override folder created (empty): {OverrideDir}");
                return;
            }

            Dictionary<string, Type> typeMap =
                GetAllDBTypes().ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

            foreach (string file in Directory.GetFiles(OverrideDir, "*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(file);

                if (!typeMap.TryGetValue(name, out Type dbType))
                {
                    MelonLogger.Warning($"[DBLoad] No DB class matches override file '{name}.json', skipping.");
                    continue;
                }

                try { ApplyOverride(dbType, file); }
                catch (Exception e)
                { MelonLogger.Error($"[DBLoad] Override failed for {name}: {e}"); }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Discovery
        // ─────────────────────────────────────────────────────────────────────

        private static IEnumerable<Type> GetAllDBTypes()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                foreach (Type t in types)
                {
                    if (t.Namespace != "DBLoad") continue;
                    if (t.GetProperty("Dic", BindingFlags.Public | BindingFlags.Static) != null)
                        yield return t;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Dump
        // ─────────────────────────────────────────────────────────────────────

        private static void DumpType(Type dbType, DumpLanguage language)
        {
            PropertyInfo dicProp =
                dbType.GetProperty("Dic", BindingFlags.Public | BindingFlags.Static);

            var dic = dicProp.GetValue(null) as IEnumerable;
            if (dic == null)
            {
                MelonLogger.Warning($"[DBLoad] {dbType.Name}.Dic returned null, skipping.");
                return;
            }

            // Collect all values from Dictionary<int, TData>
            var values = (
                from object kvp in dic
                let valueProp = kvp.GetType().GetProperty("Value")
                select valueProp.GetValue(kvp)
            ).ToList();

            // Serialize to a JArray so we can walk / mutate string tokens
            string rawJson = JsonConvert.SerializeObject(values, SerializeSettings);
            JArray array = JArray.Parse(rawJson);

            if (language != DumpLanguage.SimplifiedChinese)
                LocalizeJArray(array, (int)language);

            File.WriteAllText(
                Path.Combine(DumpDir, dbType.Name + ".json"),
                array.ToString(Formatting.Indented));

            MelonLogger.Msg(
                $"[DBLoad] Dumped {dbType.Name}: {values.Count} entries " +
                $"(language={language}) → {DumpDir}");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Localization
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Walk every string token in the array and replace it with the
        /// localized version when a translation exists in Language.Dic.
        /// </summary>
        private static void LocalizeJArray(JArray array, int languageValue)
        {
            foreach (JObject entry in array.OfType<JObject>())
                LocalizeJObject(entry, languageValue);
        }

        private static void LocalizeJObject(JObject jo, int languageValue)
        {
            foreach (JProperty prop in jo.Properties().ToList())
            {
                switch (prop.Value.Type)
                {
                    case JTokenType.String:
                        {
                            string original = prop.Value.Value<string>();
                            string localized = TryLocalize(original, languageValue);
                            if (localized != null && localized != original)
                                prop.Value = localized;
                            break;
                        }
                    // Recurse into nested objects / arrays if any data class
                    // ever contains them (future-proofing)
                    case JTokenType.Object:
                        LocalizeJObject((JObject)prop.Value, languageValue);
                        break;
                    case JTokenType.Array:
                        foreach (JObject child in ((JArray)prop.Value).OfType<JObject>())
                            LocalizeJObject(child, languageValue);
                        break;
                }
            }
        }

        /// <summary>
        /// Mirrors LanguageUtils.GetStr(string pre) via reflection.
        /// Returns null when no translation is found (caller keeps original).
        /// </summary>
        private static string TryLocalize(string original, int languageValue)
        {
            if (string.IsNullOrEmpty(original)) return null;
            if (_languageGetMethod == null) return null;

            try
            {
                // Language.Get(original.GetHashCode())
                object languageData = _languageGetMethod.Invoke(null, new object[] { original.GetHashCode() });
                if (languageData == null) return null;

                FieldInfo field = languageValue == 1 ? _traditionalField : _englishField;
                return field?.GetValue(languageData) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Finds Language.Get(int) and the LanguageData field references once.
        /// Looks for any type named "Language" in DBLoad with a static Get(int) method.
        /// </summary>
        private static void EnsureLocalizationReady()
        {
            if (_locInitialized) return;
            _locInitialized = true;

            Type languageType = null;
            Type languageDataType = null;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                foreach (Type t in types)
                {
                    if (t.Namespace == "DBLoad" && t.Name == "Language")
                        languageType = t;
                    if (t.Namespace == "DBLoad" && t.Name == "LanguageData")
                        languageDataType = t;
                }

                if (languageType != null && languageDataType != null) break;
            }

            if (languageType == null)
            {
                MelonLogger.Warning("[DBLoad] Language type not found — strings will not be localized.");
                return;
            }

            // Language.Get(int id) → LanguageData
            _languageGetMethod = languageType.GetMethod(
                "Get",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int) },
                null);

            if (_languageGetMethod == null)
            {
                MelonLogger.Warning("[DBLoad] Language.Get(int) not found.");
                return;
            }

            if (languageDataType == null)
            {
                MelonLogger.Warning("[DBLoad] LanguageData type not found.");
                return;
            }

            _traditionalField = languageDataType.GetField("m_Traditional",
                BindingFlags.Public | BindingFlags.Instance);
            _englishField = languageDataType.GetField("m_English",
                BindingFlags.Public | BindingFlags.Instance);

            if (_traditionalField == null || _englishField == null)
                MelonLogger.Warning("[DBLoad] LanguageData fields not found (m_Traditional / m_English).");
            else
                MelonLogger.Msg("[DBLoad] Localization ready.");
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Override loading (unchanged from before)
        // ─────────────────────────────────────────────────────────────────────

        private static void ApplyOverride(Type dbType, string filePath)
        {
            PropertyInfo dicProp =
                dbType.GetProperty("Dic", BindingFlags.Public | BindingFlags.Static);

            object dic = dicProp.GetValue(null);
            if (dic == null) return;

            Type dicType = dic.GetType();
            Type dataType = dicType.GetGenericArguments()[1];
            MethodInfo setItem = dicType.GetMethod("set_Item");
            MethodInfo containsKey = dicType.GetMethod("ContainsKey");

            JArray entries = JArray.Parse(File.ReadAllText(filePath));
            int replaced = 0, added = 0;

            foreach (JObject entry in entries)
            {
                JToken idToken = entry["m_id"];
                if (idToken == null) continue;

                int id = idToken.Value<int>();
                object dataObj = ReconstructFromJObject(dataType, entry);
                if (dataObj == null) continue;

                bool exists = (bool)containsKey.Invoke(dic, new object[] { id });
                setItem.Invoke(dic, new object[] { id, dataObj });

                if (exists) replaced++; else added++;
            }

            MelonLogger.Msg($"[DBLoad] {dbType.Name}: {replaced} replaced, {added} added.");
        }

        private static object ReconstructFromJObject(Type dataType, JObject jo)
        {
            ConstructorInfo ctor = dataType
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(c => c.GetParameters().Length)
                .First();

            ParameterInfo[] parameters = ctor.GetParameters();
            object[] args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                // "_id" → "m_id",  "_npcId" → "m_npcId", etc.
                string fieldName = "m_" + parameters[i].Name.TrimStart('_');
                JToken token = jo[fieldName] ?? jo[parameters[i].Name];

                args[i] = token != null
                    ? token.ToObject(parameters[i].ParameterType)
                    : GetDefaultValue(parameters[i].ParameterType);
            }

            return ctor.Invoke(args);
        }

        private static object GetDefaultValue(Type t) =>
            t.IsValueType ? Activator.CreateInstance(t) : null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Serialize public readonly fields
    // ─────────────────────────────────────────────────────────────────────────
    public class PublicFieldsContractResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(
            Type type, MemberSerialization memberSerialization)
        {
            var props = new List<JsonProperty>();
            foreach (FieldInfo fi in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                JsonProperty jp = base.CreateProperty(fi, memberSerialization);
                jp.Readable = true;
                jp.Writable = true;
                props.Add(jp);
            }
            return props;
        }
    }
}