using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectInitializer
{
    /// <summary>
    /// 预设管理器 — 预设的增删改查、默认预设生成、序列化。
    /// </summary>
    public static class PresetManager
    {
        public const string PresetFolder = "Assets/ProjectInitializer/Presets";
        private const string DefaultPresetName = "DefaultGameProjectPreset";

        /// <summary>
        /// 查找项目中所有预设资产。
        /// </summary>
        public static List<ProjectInitPreset> FindAllPresets()
        {
            var presets = new List<ProjectInitPreset>();
            var guids = AssetDatabase.FindAssets($"t:{nameof(ProjectInitPreset)}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<ProjectInitPreset>(path);
                if (preset != null)
                    presets.Add(preset);
            }
            return presets;
        }

        /// <summary>
        /// 加载指定路径的预设。
        /// </summary>
        public static ProjectInitPreset LoadPreset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
                return null;
            return AssetDatabase.LoadAssetAtPath<ProjectInitPreset>(assetPath);
        }

        /// <summary>
        /// 保存预设到磁盘。
        /// </summary>
        public static string SavePreset(ProjectInitPreset preset, string fileName = null)
        {
            if (!Directory.Exists(PresetFolder))
                Directory.CreateDirectory(PresetFolder);

            string name = string.IsNullOrEmpty(fileName) ? preset.presetName : fileName;
            string assetPath = $"{PresetFolder}/{name}.asset";

            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            AssetDatabase.CreateAsset(preset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return assetPath;
        }

        /// <summary>
        /// 删除预设资产。
        /// </summary>
        public static bool DeletePreset(ProjectInitPreset preset)
        {
            if (preset == null) return false;
            string path = AssetDatabase.GetAssetPath(preset);
            if (string.IsNullOrEmpty(path)) return false;

            bool success = AssetDatabase.DeleteAsset(path);
            if (success)
                AssetDatabase.Refresh();
            return success;
        }

        /// <summary>
        /// 创建一个内置的默认游戏项目预设。
        /// </summary>
        public static ProjectInitPreset CreateDefaultPreset()
        {
            var preset = ScriptableObject.CreateInstance<ProjectInitPreset>();
            preset.presetName = DefaultPresetName;
            preset.description = "标准游戏项目初始化预设\n包含常用目录结构、常用依赖包和基本项目设置。";

            // 标准游戏项目目录
            preset.directories = new List<DirectoryEntry>
            {
                new DirectoryEntry("Animations"),
                new DirectoryEntry("Audio/Music"),
                new DirectoryEntry("Audio/SFX"),
                new DirectoryEntry("Materials"),
                new DirectoryEntry("Models"),
                new DirectoryEntry("Prefabs"),
                new DirectoryEntry("Scenes"),
                new DirectoryEntry("Scripts/Runtime"),
                new DirectoryEntry("Scripts/Editor"),
                new DirectoryEntry("Settings"),
                new DirectoryEntry("Shaders"),
                new DirectoryEntry("Sprites"),
                new DirectoryEntry("Textures"),
                new DirectoryEntry("UI"),
            };

            // 常用 UPM 包
            preset.packages = new List<PackageEntry>
            {
                new PackageEntry("com.unity.addressables", "Addressables", "com.unity.addressables"),
                new PackageEntry("com.unity.nuget.newtonsoft-json", "Newtonsoft Json", "com.unity.nuget.newtonsoft-json"),
                new PackageEntry("com.cysharp.unitask", "UniTask", "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"),
                // UnityFramework 系列
                new PackageEntry("com.unityframework.core", "UnityFramework",
                    "https://gitee.com/PN-BUG/infinite-treasury.git?path=Assets/UnityFramework", false),
                new PackageEntry("com.zko.zeventsystem", "ZEventSystem",
                    "https://gitee.com/PN-BUG/infinite-treasury.git?path=Assets/UnityFramework/Runtime/ZEventSystem", false),
                new PackageEntry("com.zko.unitytoolshub", "UnityToolsHub",
                    "https://gitee.com/PN-BUG/infinite-treasury.git?path=Assets/UnityFramework/Editor/UnityToolsHub", false),
            };

            // 基本项目设置
            preset.settings = new List<SettingsEntry>
            {
                new SettingsEntry(SettingsEntry.Category.PlayerName, "companyName", "DefaultCompany"),
                new SettingsEntry(SettingsEntry.Category.PlayerName, "productName", "GameProject"),
                new SettingsEntry(SettingsEntry.Category.PlayerName, "bundleVersion", "0.1.0"),
            };

            return preset;
        }

        /// <summary>
        /// 创建空白预设。
        /// </summary>
        public static ProjectInitPreset CreateEmptyPreset()
        {
            var preset = ScriptableObject.CreateInstance<ProjectInitPreset>();
            preset.presetName = "NewPreset";
            preset.description = "空白预设";
            preset.directories = new List<DirectoryEntry>();
            preset.packages = new List<PackageEntry>();
            preset.settings = new List<SettingsEntry>();
            return preset;
        }

        /// <summary>
        /// 确保预设文件夹存在。
        /// </summary>
        public static void EnsurePresetFolder()
        {
            if (!Directory.Exists(PresetFolder))
                Directory.CreateDirectory(PresetFolder);
            // 确保 .meta 文件生成
            AssetDatabase.Refresh();
        }
    }
}
