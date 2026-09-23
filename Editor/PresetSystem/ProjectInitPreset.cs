using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectInitializer
{
    /// <summary>
    /// 项目初始化预设 — 存储目录模板、依赖包、项目设置的完整配置。
    /// 通过预设编辑器创建并保存到本地预设目录。
    /// </summary>
    [Serializable]
    public class ProjectInitPreset : ScriptableObject
    {
        [Tooltip("预设显示名称")]
        public string presetName = "New Preset";

        [Tooltip("预设描述")]
        [TextArea(2, 6)]
        public string description = string.Empty;

        [Tooltip("目录模板 — 要在 Assets 下创建的文件夹列表")]
        public List<DirectoryEntry> directories = new List<DirectoryEntry>();

        [Tooltip("依赖包 — 需要安装的 UPM 包列表")]
        public List<PackageEntry> packages = new List<PackageEntry>();

        [Tooltip("Assets/Plugins 中可选择完整复制的目录或文件")]
        public List<PluginEntry> plugins = new List<PluginEntry>();

        [Tooltip("与预设一起复制到新项目的插件文件归档")]
        public TextAsset pluginArchive;

        [HideInInspector]
        public string importedFromPath;

        [NonSerialized]
        public string sourceProjectRoot;

        [Tooltip("项目设置 — 需要应用的 PlayerSettings 配置")]
        public List<SettingsEntry> settings = new List<SettingsEntry>();

        /// <summary>
        /// 创建预设的深拷贝（用于编辑时避免修改原始数据）。
        /// </summary>
        public ProjectInitPreset Clone()
        {
            var clone = CreateInstance<ProjectInitPreset>();
            clone.presetName = presetName;
            clone.description = description;
            clone.directories = directories?.ConvertAll(d => d == null ? null : new DirectoryEntry(d.path, d.enabled)) ?? new List<DirectoryEntry>();
            clone.packages = packages?.ConvertAll(p => p == null ? null : new PackageEntry(p.packageName, p.displayName, p.installSpec, p.selected)) ?? new List<PackageEntry>();
            clone.plugins = plugins?.ConvertAll(p => p == null ? null : new PluginEntry(p.path, p.isDirectory, p.copyFiles)) ?? new List<PluginEntry>();
            clone.pluginArchive = pluginArchive;
            clone.importedFromPath = importedFromPath;
            clone.sourceProjectRoot = sourceProjectRoot;
            clone.settings = settings?.ConvertAll(s => s == null ? null : new SettingsEntry(s.category, s.key, s.value, s.enabled)) ?? new List<SettingsEntry>();
            return clone;
        }

        /// <summary>
        /// 获取启用的目录条目数量。
        /// </summary>
        public int EnabledDirectoryCount
        {
            get
            {
                int count = 0;
                if (directories == null) return 0;
                foreach (var d in directories)
                    if (d != null && d.enabled) count++;
                return count;
            }
        }

        /// <summary>
        /// 获取选中的依赖包数量。
        /// </summary>
        public int SelectedPackageCount
        {
            get
            {
                int count = 0;
                if (packages == null) return 0;
                foreach (var p in packages)
                    if (p != null && p.selected) count++;
                return count;
            }
        }

        /// <summary>
        /// 获取启用的设置项数量。
        /// </summary>
        public int EnabledSettingsCount
        {
            get
            {
                int count = 0;
                if (settings == null) return 0;
                foreach (var s in settings)
                    if (s != null && s.enabled) count++;
                return count;
            }
        }

        public int SelectedPluginCount
        {
            get
            {
                int count = 0;
                if (plugins == null) return 0;
                foreach (var p in plugins)
                    if (p != null && p.copyFiles) count++;
                return count;
            }
        }
    }

    [Serializable]
    public class PluginEntry
    {
        [Tooltip("相对于 Assets 的路径，必须位于 Plugins 下")]
        public string path;

        [Tooltip("是否为目录")]
        public bool isDirectory;

        [Tooltip("应用预设时复制完整文件和 .meta")]
        public bool copyFiles;

        public PluginEntry() { }

        public PluginEntry(string path, bool isDirectory, bool copyFiles = false)
        {
            this.path = path;
            this.isDirectory = isDirectory;
            this.copyFiles = copyFiles;
        }
    }

    [Serializable]
    public class DirectoryEntry
    {
        [Tooltip("相对于 Assets 的路径，如 Scripts/Runtime 或 Art/Textures")]
        public string path;

        [Tooltip("是否启用此项")]
        public bool enabled = true;

        public DirectoryEntry() { }

        public DirectoryEntry(string path, bool enabled = true)
        {
            this.path = path;
            this.enabled = enabled;
        }
    }

    [Serializable]
    public class PackageEntry
    {
        [Tooltip("UPM 包名，如 com.unity.addressables")]
        public string packageName;

        [Tooltip("显示名称")]
        public string displayName;

        [Tooltip("安装规格 — 包名或 Git URL")]
        public string installSpec;

        [Tooltip("是否选中安装")]
        public bool selected = true;

        public PackageEntry() { }

        public PackageEntry(string packageName, string displayName, string installSpec, bool selected = true)
        {
            this.packageName = packageName;
            this.displayName = displayName;
            this.installSpec = installSpec;
            this.selected = selected;
        }
    }

    [Serializable]
    public class SettingsEntry
    {
        public enum Category
        {
            PlayerName,        // companyName / productName / bundleVersion
            ScriptingDefines,  // 添加 Scripting Define Symbols
            Android,           // Android 专属设置
            iOS,               // iOS 专属设置
            Graphics,          // 渲染管线相关
            Custom             // 自定义 key-value（通过反射应用）
        }

        [Tooltip("设置分类")]
        public Category category;

        [Tooltip("设置键名，如 companyName / productName / bundleVersion")]
        public string key;

        [Tooltip("设置值")]
        public string value;

        [Tooltip("是否启用此项")]
        public bool enabled = true;

        public SettingsEntry() { }

        public SettingsEntry(Category category, string key, string value, bool enabled = true)
        {
            this.category = category;
            this.key = key;
            this.value = value;
            this.enabled = enabled;
        }
    }
}
