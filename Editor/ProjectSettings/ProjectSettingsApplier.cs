using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ProjectInitializer
{
    /// <summary>
    /// 项目设置应用器 — 将预设中的设置项应用到 PlayerSettings。
    /// </summary>
    public static class ProjectSettingsApplier
    {
        public struct ApplyResult
        {
            public int appliedCount;
            public int failedCount;
            public List<string> appliedSettings;
            public List<string> failedSettings;

            public override string ToString()
            {
                return $"已应用 {appliedCount} 项设置" + (failedCount > 0 ? $"，失败 {failedCount} 项" : "");
            }
        }

        /// <summary>
        /// 应用设置项列表。
        /// </summary>
        public static ApplyResult Apply(List<SettingsEntry> entries)
        {
            var result = new ApplyResult
            {
                appliedSettings = new List<string>(),
                failedSettings = new List<string>()
            };

            if (entries == null || entries.Count == 0)
                return result;

            foreach (var entry in entries)
            {
                if (entry == null || !entry.enabled)
                    continue;

                bool success = TryApplySetting(entry);
                if (success)
                {
                    result.appliedCount++;
                    result.appliedSettings.Add($"{entry.category}/{entry.key} = {entry.value}");
                }
                else
                {
                    result.failedCount++;
                    result.failedSettings.Add($"{entry.category}/{entry.key}");
                }
            }

            return result;
        }

        private static bool TryApplySetting(SettingsEntry entry)
        {
            try
            {
                switch (entry.category)
                {
                    case SettingsEntry.Category.PlayerName:
                        return ApplyPlayerNameSetting(entry.key, entry.value);

                    case SettingsEntry.Category.ScriptingDefines:
                        return ApplyScriptingDefine(entry.key, entry.value);

                    case SettingsEntry.Category.Android:
                        return ApplyAndroidSetting(entry.key, entry.value);

                    case SettingsEntry.Category.iOS:
                        return ApplyiOSSetting(entry.key, entry.value);

                    case SettingsEntry.Category.Graphics:
                        return ApplyGraphicsSetting(entry.key, entry.value);

                    case SettingsEntry.Category.Custom:
                        return ApplyCustomSetting(entry.key, entry.value);

                    default:
                        Debug.LogWarning($"[ProjectInitializer] 未知设置分类: {entry.category}");
                        return false;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProjectInitializer] 应用设置失败: {entry.category}/{entry.key}\n{e.Message}");
                return false;
            }
        }

        #region PlayerName

        private static bool ApplyPlayerNameSetting(string key, string value)
        {
            switch (key?.ToLowerInvariant())
            {
                case "companyname":
                case "company":
                    PlayerSettings.companyName = value;
                    return true;

                case "productname":
                case "product":
                    PlayerSettings.productName = value;
                    return true;

                case "bundleversion":
                case "version":
                    PlayerSettings.bundleVersion = value;
                    return true;

                default:
                    Debug.LogWarning($"[ProjectInitializer] 未知的 PlayerName 设置键: {key}");
                    return false;
            }
        }

        #endregion

        #region ScriptingDefines

        private static bool ApplyScriptingDefine(string symbol, string value)
        {
            // value 为 "true" 或 "false" 决定是否添加该 define
            bool enable = string.IsNullOrEmpty(value) || value.Equals("true", System.StringComparison.OrdinalIgnoreCase);

            foreach (BuildTargetGroup group in System.Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (group == BuildTargetGroup.Unknown) continue;

                NamedBuildTarget namedTarget;
                try { namedTarget = NamedBuildTarget.FromBuildTargetGroup(group); }
                catch { continue; }

                var defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
                var tokens = defines.Split(';', System.StringSplitOptions.RemoveEmptyEntries);
                var list = new List<string>(tokens.Select(t => t.Trim()));

                if (enable)
                {
                    if (!list.Contains(symbol))
                        list.Add(symbol);
                }
                else
                {
                    list.RemoveAll(d => d == symbol);
                }

                PlayerSettings.SetScriptingDefineSymbols(namedTarget, string.Join(";", list));
            }

            return true;
        }

        #endregion

        #region Android

        private static bool ApplyAndroidSetting(string key, string value)
        {
            switch (key?.ToLowerInvariant())
            {
                case "minsdkversion":
                case "minsdk":
                    if (int.TryParse(value, out int minSdk))
                    {
                        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)minSdk;
                        return true;
                    }
                    return false;

                case "targetsdkversion":
                case "targetsdk":
                    if (int.TryParse(value, out int targetSdk))
                    {
                        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)targetSdk;
                        return true;
                    }
                    return false;

                case "packagename":
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, value);
                    return true;

                case "scriptingbackend":
                    if (value.Equals("il2cpp", System.StringComparison.OrdinalIgnoreCase))
                        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                    else
                        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.Mono2x);
                    return true;

                case "apiversion":
                    if (value.Equals("il2cpp", System.StringComparison.OrdinalIgnoreCase))
                        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
                    else
                        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_4_6);
                    return true;

                default:
                    Debug.LogWarning($"[ProjectInitializer] 未知的 Android 设置键: {key}");
                    return false;
            }
        }

        #endregion

        #region iOS

        private static bool ApplyiOSSetting(string key, string value)
        {
            switch (key?.ToLowerInvariant())
            {
                case "packagename":
                case "bundleidentifier":
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, value);
                    return true;

                case "targetiosversion":
                case "targetversion":
                    PlayerSettings.iOS.targetOSVersionString = value;
                    return true;

                case "scriptingbackend":
                    if (value.Equals("il2cpp", System.StringComparison.OrdinalIgnoreCase))
                        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
                    else
                        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.Mono2x);
                    return true;

                case "cameradescription":
                    PlayerSettings.iOS.cameraUsageDescription = value;
                    return true;

                case "microphonedescription":
                    PlayerSettings.iOS.microphoneUsageDescription = value;
                    return true;

                default:
                    Debug.LogWarning($"[ProjectInitializer] 未知的 iOS 设置键: {key}");
                    return false;
            }
        }

        #endregion

        #region Graphics

        private static bool ApplyGraphicsSetting(string key, string value)
        {
            switch (key?.ToLowerInvariant())
            {
                case "colorspace":
                    bool linear = value.Equals("linear", System.StringComparison.OrdinalIgnoreCase);
                    PlayerSettings.colorSpace = linear ? ColorSpace.Linear : ColorSpace.Gamma;
                    return true;

                case "usetiasafeMode":
                case "usegfxdeviceasync":
                    // 占位：可按需扩展
                    return true;

                default:
                    Debug.LogWarning($"[ProjectInitializer] 未知的 Graphics 设置键: {key}");
                    return false;
            }
        }

        #endregion

        #region Custom

        private static bool ApplyCustomSetting(string key, string value)
        {
            // 尝试通过反射设置 PlayerSettings 上的静态属性
            var prop = typeof(PlayerSettings).GetProperty(key,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (prop != null && prop.CanWrite)
            {
                object convertedValue = ConvertValue(value, prop.PropertyType);
                if (convertedValue != null)
                {
                    prop.SetValue(null, convertedValue);
                    return true;
                }
            }

            // 尝试字段
            var field = typeof(PlayerSettings).GetField(key,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (field != null)
            {
                object convertedValue = ConvertValue(value, field.FieldType);
                if (convertedValue != null)
                {
                    field.SetValue(null, convertedValue);
                    return true;
                }
            }

            Debug.LogWarning($"[ProjectInitializer] 无法找到 PlayerSettings 上的属性/字段: {key}");
            return false;
        }

        private static object ConvertValue(string value, System.Type targetType)
        {
            try
            {
                if (targetType == typeof(string))
                    return value;
                if (targetType == typeof(int))
                    return int.Parse(value);
                if (targetType == typeof(float))
                    return float.Parse(value);
                if (targetType == typeof(bool))
                    return bool.Parse(value);
                if (targetType.IsEnum)
                    return System.Enum.Parse(targetType, value, true);
                return System.Convert.ChangeType(value, targetType);
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
