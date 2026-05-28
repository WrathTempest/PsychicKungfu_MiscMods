using DBLoad;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace PsychicKungfu_MelonMod.Utils
{
    internal static class Helpers
    {
        public static bool IsPlayer(Role role)
        {
            return (role.m_data.m_id > 0 && role.m_data.m_id < 7);
        }

        public static bool HasBuff(Role role, int id)
        {
            List<int> ids = new List<int>();
            foreach (BuffInfo buff in role.m_buffs)
            {
                ids.Add(buff.Id);
            }
            return ids.Contains(id);

        }
        public static bool HasPassive(Role role, int id)
        {
            List<int> ids = new List<int>();
            foreach (PassiveSkill passive in role.m_passives)
            {
                //MelonLogger.Msg($"Role ID: {role.m_data.m_id} Current passive ID: {passive.m_id}");

                ids.Add(passive.m_id);
            }
            return ids.Contains(id);

        }

        public static void AddAllSkillManuals()
        {
            SaveData saveData = MonoSingleton<SaveManager>.Instance.SaveData;
            List<ItemInfo> items = new List<ItemInfo>();
            foreach (WuXueData skill in WuXue.Dic.Values)
            {
                if (Item.Get(skill.m_id) != null && saveData.GetItemNum(skill.m_id) <= 0)
                {
                    items.Add(new ItemInfo(skill.m_id, 1));
                }
            }
            saveData.AddItems(items, true);
        }
        public static bool PlayerHasGlobalBuff(int id)
        {          
            SaveData saveData = MonoSingleton<SaveManager>.Instance.SaveData;
            return saveData.m_globalBuffHash.Contains(id);
        }

        public static bool PlayerHasGlobalPassive(int id)
        {
            SaveData saveData = MonoSingleton<SaveManager>.Instance.SaveData;
            return saveData.m_passives.Contains(id);
        }

        public static void LearnAllSkills()
        {
            SaveData saveData = MonoSingleton<SaveManager>.Instance.SaveData;
            foreach (WuXueData skill in WuXue.Dic.Values)
            {
                saveData.ReadWuXue(skill.m_id);
            }
        }

        public static T GetPrivateField<T>(object instance, string fieldName)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));

            Type type = instance.GetType();
            FieldInfo field = null;

            while (type != null)
            {
                field = type.GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public |
                    BindingFlags.Static);

                if (field != null)
                    break;

                type = type.BaseType;
            }

            if (field == null)
            {
                MelonLogger.Msg(
                    $"[Helpers] Failed GetPrivateField<{typeof(T).Name}> " +
                    $"Field='{fieldName}' Type='{instance.GetType().FullName}'");

                throw new MissingFieldException(
                    instance.GetType().FullName,
                    fieldName);
            }

            object value = field.GetValue(instance);

            return (T)value;
        }

        public static void SetPrivateField<T>(
            object instance,
            string fieldName,
            T newValue)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));

            Type type = instance.GetType();
            FieldInfo field = null;

            while (type != null)
            {
                field = type.GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public |
                    BindingFlags.Static);

                if (field != null)
                    break;

                type = type.BaseType;
            }

            if (field == null)
            {
                MelonLogger.Msg(
                    $"[Helpers] Failed SetPrivateField<{typeof(T).Name}> " +
                    $"Field='{fieldName}' Type='{instance.GetType().FullName}'");

                throw new MissingFieldException(
                    instance.GetType().FullName,
                    fieldName);
            }

            field.SetValue(instance, newValue);

        }

        public static T GetPrivateProperty<T>(
            object instance,
            string propertyName)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Type type = instance.GetType();

            while (type != null)
            {
                PropertyInfo prop = type.GetProperty(
                    propertyName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public |
                    BindingFlags.Static);

                if (prop != null)
                {
                    object value = prop.GetValue(instance, null);

                    return (T)value;
                }

                MethodInfo getter = type.GetMethod(
                    "get_" + propertyName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public |
                    BindingFlags.Static);

                if (getter != null)
                {
                    object value = getter.Invoke(instance, null);

                    return (T)value;
                }

                type = type.BaseType;
            }

            MelonLogger.Msg(
                $"[Helpers] Failed GetPrivateProperty<{typeof(T).Name}> " +
                $"Property='{propertyName}' " +
                $"Type='{instance.GetType().FullName}'");

            throw new MissingMemberException(
                instance.GetType().FullName,
                propertyName);
        }

        public static void SetPrivateProperty<T>(
            object instance,
            string propertyName,
            T value)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Type type = instance.GetType();

            while (type != null)
            {
                PropertyInfo prop = type.GetProperty(
                    propertyName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public |
                    BindingFlags.Static);

                if (prop != null)
                {
                    prop.SetValue(instance, value, null);

                    return;
                }

                MethodInfo setter = type.GetMethod(
                    "set_" + propertyName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public |
                    BindingFlags.Static);

                if (setter != null)
                {
                    setter.Invoke(instance, new object[] { value });

                    return;
                }

                type = type.BaseType;
            }

            MelonLogger.Msg(
                $"[Helpers] Failed SetPrivateProperty<{typeof(T).Name}> " +
                $"Property='{propertyName}' " +
                $"Type='{instance.GetType().FullName}'");

            throw new MissingMemberException(
                instance.GetType().FullName,
                propertyName);
        }

        public static string GetRealCaller(int skipFrames = 1)
        {
            StackTrace stackTrace = new StackTrace(skipFrames, true);

            foreach (StackFrame frame in stackTrace.GetFrames())
            {
                MethodBase method = frame.GetMethod();

                if (method == null)
                    continue;

                if (method.Name.Contains("DMD<"))
                    continue;

                if (method.DeclaringType == typeof(Helpers))
                    continue;

                return $"{method.DeclaringType?.FullName}.{method.Name}";
            }

            return "UnknownCaller";
        }

        public static void LogCaller(
            string message = "",
            int skipFrames = 1)
        {
            string caller = GetRealCaller(skipFrames + 1);

            MelonLogger.Msg(
                $"[Helpers] {message} Called by: {caller}");
        }

        public static object Call(
            object instance,
            string methodName,
            Type[] paramTypes,
            params object[] args)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Type type = instance.GetType();

            MethodInfo method = FindMethod(
                type,
                methodName,
                paramTypes);

            if (method == null)
            {
                MelonLogger.Msg(
                    $"[Helpers] Failed Call Method='{methodName}' " +
                    $"Type='{type.FullName}'");

                throw new MissingMethodException(
                    type.FullName,
                    methodName);
            }

            MelonLogger.Msg(
                $"[Helpers] Call Method='{methodName}' " +
                $"Type='{type.FullName}'");

            return method.Invoke(instance, args);
        }

        public static object Call(
            object instance,
            string methodName,
            params object[] args)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Type type = instance.GetType();

            MethodInfo method = FindMethod(
                type,
                methodName);

            if (method == null)
            {
                MelonLogger.Msg(
                    $"[Helpers] Failed Call Method='{methodName}' " +
                    $"Type='{type.FullName}'");

                throw new MissingMethodException(
                    type.FullName,
                    methodName);
            }

            MelonLogger.Msg(
                $"[Helpers] Call Method='{methodName}' " +
                $"Type='{type.FullName}'");

            return method.Invoke(instance, args);
        }

        public static object CallStatic(
            Type type,
            string methodName,
            params object[] args)
        {
            MethodInfo method = FindMethod(
                type,
                methodName);

            if (method == null)
            {
                MelonLogger.Msg(
                    $"[Helpers] Failed CallStatic Method='{methodName}' " +
                    $"Type='{type.FullName}'");

                throw new MissingMethodException(
                    type.FullName,
                    methodName);
            }

            MelonLogger.Msg(
                $"[Helpers] CallStatic Method='{methodName}' " +
                $"Type='{type.FullName}'");

            return method.Invoke(null, args);
        }

        private static MethodInfo FindMethod(
            Type type,
            string methodName,
            Type[] paramTypes = null)
        {
            while (type != null)
            {
                MethodInfo method;

                if (paramTypes != null)
                {
                    method = type.GetMethod(
                        methodName,
                        BindingFlags.Instance |
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic,
                        null,
                        paramTypes,
                        null);
                }
                else
                {
                    method = type.GetMethod(
                        methodName,
                        BindingFlags.Instance |
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);
                }

                if (method != null)
                    return method;

                type = type.BaseType;
            }

            return null;
        }

    }
}
