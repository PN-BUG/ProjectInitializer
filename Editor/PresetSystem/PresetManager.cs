using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        public const string UnityFrameworkInstallSpec = "https://gitee.com/PN-BUG/infinite-treasury.git?path=Assets/UnityFramework";
        private const string DefaultPresetName = "DefaultGameProjectPreset";
        public static string CurrentProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [Serializable]
        private class PackageMetadata
        {
            public string name;
            public string displayName;
        }

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
            presets.Sort((a, b) => string.Compare(a.presetName, b.presetName, StringComparison.OrdinalIgnoreCase));
            return presets;
        }

        /// <summary>
        /// 加载指定路径的预设。
        /// </summary>
        public static ProjectInitPreset LoadPreset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;
            return AssetDatabase.LoadAssetAtPath<ProjectInitPreset>(assetPath);
        }

        /// <summary>
        /// 保存预设到磁盘。
        /// </summary>
        public static string SavePreset(ProjectInitPreset preset, string fileName = null)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            EnsurePresetFolder();

            string name = string.IsNullOrEmpty(fileName) ? preset.presetName : fileName;
            name = Path.GetFileNameWithoutExtension(name);
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("预设文件名无效。", nameof(fileName));
            string assetPath = $"{PresetFolder}/{name}.asset";

            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            AssetDatabase.CreateAsset(preset, assetPath);
            PluginArchiveManager.RefreshArchive(preset);
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
            if (string.IsNullOrEmpty(path) || !path.StartsWith(PresetFolder + "/", StringComparison.Ordinal)) return false;

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
                    UnityFrameworkInstallSpec, false),
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
            preset.plugins = new List<PluginEntry>();
            preset.settings = new List<SettingsEntry>();
            return preset;
        }

        /// <summary>捕获当前项目的前两层 Assets 目录、直接 UPM 依赖、本地包和基本 Player 设置。</summary>
        public static ProjectInitPreset CreateFromCurrentProject()
            => CreateFromProject(CurrentProjectRoot);

        public static bool IsUnityProject(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) return false;
            return Directory.Exists(Path.Combine(projectRoot, "Assets")) &&
                   File.Exists(Path.Combine(projectRoot, "Packages", "manifest.json")) &&
                   File.Exists(Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt"));
        }

        /// <summary>从指定 Unity 项目读取目录、包、插件和基本 Player 设置。</summary>
        public static ProjectInitPreset CreateFromProject(string projectRoot)
        {
            projectRoot = Path.GetFullPath(projectRoot);
            if (!IsUnityProject(projectRoot)) throw new ArgumentException("请选择包含 Assets、Packages 和 ProjectSettings 的 Unity 项目根目录。", nameof(projectRoot));
            var preset = CreateEmptyPreset();
            preset.sourceProjectRoot = projectRoot;
            string settingsPath = Path.Combine(projectRoot, "ProjectSettings", "ProjectSettings.asset");
            string settingsText = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : string.Empty;
            string productName = ReadYamlScalar(settingsText, "productName");
            preset.presetName = (string.IsNullOrEmpty(productName) ? Path.GetFileName(projectRoot) : productName) + "Preset";
            preset.description = "由项目目录生成。目录仅包含前两层；Assets 下的本地包默认不安装，请保存前检查来源。";

            string assetsPath = Path.Combine(projectRoot, "Assets");
            foreach (var top in Directory.GetDirectories(assetsPath).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                if (Path.GetFileName(top).StartsWith(".", StringComparison.Ordinal)) continue;
                if (string.Equals(Path.GetFileName(top), "Plugins", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(Path.GetFileName(top), "ProjectInitializer", StringComparison.OrdinalIgnoreCase)) continue;
                preset.directories.Add(new DirectoryEntry(Path.GetFileName(top)));
                foreach (var child in Directory.GetDirectories(top).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    if (Path.GetFileName(child).StartsWith(".", StringComparison.Ordinal)) continue;
                    preset.directories.Add(new DirectoryEntry(Path.GetFileName(top) + "/" + Path.GetFileName(child)));
                }
            }

            preset.packages = ReadProjectPackages(projectRoot);
            preset.plugins = ReadProjectPlugins(projectRoot);
            AddSetting(preset, "companyName", ReadYamlScalar(settingsText, "companyName"));
            AddSetting(preset, "productName", productName);
            AddSetting(preset, "bundleVersion", ReadYamlScalar(settingsText, "bundleVersion"));
            string color = ReadYamlScalar(settingsText, "m_ActiveColorSpace");
            if (color == "0" || color == "1")
                preset.settings.Add(new SettingsEntry(SettingsEntry.Category.Graphics, "colorSpace", color == "1" ? "Linear" : "Gamma"));
            return preset;
        }

        private static void AddSetting(ProjectInitPreset preset, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
                preset.settings.Add(new SettingsEntry(SettingsEntry.Category.PlayerName, key, value));
        }

        private static string ReadYamlScalar(string yaml, string key)
        {
            var match = Regex.Match(yaml, @"(?m)^\s*" + Regex.Escape(key) + @":\s*(?<value>.*)$");
            return match.Success ? match.Groups["value"].Value.Trim().Trim('"') : string.Empty;
        }

        /// <summary>只读取 manifest.json 的直接依赖，保留版本和 Git URL。</summary>
        public static List<PackageEntry> ReadDirectPackages()
            => ReadDirectPackages(CurrentProjectRoot);

        public static List<PackageEntry> ReadDirectPackages(string projectRoot)
        {
            var result = new List<PackageEntry>();
            string path = Path.Combine(projectRoot, "Packages", "manifest.json");
            if (!File.Exists(path)) return result;
            string json = File.ReadAllText(path);
            var match = Regex.Match(json, "\\\"dependencies\\\"\\s*:\\s*\\{");
            if (!match.Success) return result;
            int start = match.Index + match.Length;
            int end = json.IndexOf('}', start);
            if (end < start) return result;
            string dependencies = json.Substring(start, end - start);
            foreach (Match item in Regex.Matches(dependencies, @"""(?<name>[^""]+)""\s*:\s*""(?<spec>[^""]+)"""))
            {
                string name = item.Groups["name"].Value;
                string spec = item.Groups["spec"].Value;
                if (name.StartsWith("com.unity.modules.", StringComparison.Ordinal)) continue;
                bool portable = !spec.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
                string installSpec = spec.Contains("://") || spec.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                    ? spec : name + "@" + spec;
                result.Add(new PackageEntry(name, name, installSpec, portable));
            }
            return result;
        }

        /// <summary>合并 manifest 直接依赖和 Assets 下的顶层 package.json 包。</summary>
        public static List<PackageEntry> ReadProjectPackages()
            => ReadProjectPackages(CurrentProjectRoot);

        public static List<PackageEntry> ReadProjectPackages(string projectRoot)
        {
            var result = ReadDirectPackages(projectRoot);
            foreach (var package in ReadAssetPackages(projectRoot))
                if (!result.Any(p => p.packageName == package.packageName))
                    result.Add(package);
            return result;
        }

        /// <summary>读取 Assets 中最外层的 package.json，避免将一个包的内嵌子目录重复视为独立安装项。</summary>
        public static List<PackageEntry> ReadAssetPackages()
            => ReadAssetPackages(CurrentProjectRoot);

        public static List<PackageEntry> ReadAssetPackages(string projectRoot)
        {
            var result = new List<PackageEntry>();
            string assetsPath = Path.Combine(projectRoot, "Assets");
            if (!Directory.Exists(assetsPath)) return result;
            var packageRoots = new List<string>();
            foreach (string file in Directory.GetFiles(assetsPath, "package.json", SearchOption.AllDirectories)
                         .OrderBy(p => p.Length))
            {
                string root = Path.GetDirectoryName(file);
                if (packageRoots.Any(parent => root.StartsWith(parent + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))) continue;

                PackageMetadata metadata;
                try { metadata = JsonUtility.FromJson<PackageMetadata>(File.ReadAllText(file)); }
                catch (Exception) { continue; }
                if (metadata == null || string.IsNullOrWhiteSpace(metadata.name)) continue;
                packageRoots.Add(root);
                if (result.Any(p => p.packageName == metadata.name)) continue;

                string relativePath = "Assets/" + Path.GetRelativePath(assetsPath, root).Replace('\\', '/');
                string source = metadata.name == "com.unityframework.core"
                    ? UnityFrameworkInstallSpec : "file:../" + relativePath;
                result.Add(new PackageEntry(metadata.name,
                    string.IsNullOrWhiteSpace(metadata.displayName) ? metadata.name : metadata.displayName,
                    source, false));
            }
            return result;
        }

        /// <summary>读取 Assets/Plugins 的顶层插件目录和独立文件。</summary>
        public static List<PluginEntry> ReadCurrentProjectPlugins()
            => ReadProjectPlugins(CurrentProjectRoot);

        public static List<PluginEntry> ReadProjectPlugins(string projectRoot)
        {
            var result = new List<PluginEntry>();
            string root = Path.Combine(projectRoot, "Assets", "Plugins");
            if (!Directory.Exists(root)) return result;
            foreach (string path in Directory.GetFileSystemEntries(root).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileName(path);
                if (name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;
                result.Add(new PluginEntry("Plugins/" + name, Directory.Exists(path)));
            }
            return result;
        }

        /// <summary>
        /// 确保预设文件夹存在。
        /// </summary>
        public static void EnsurePresetFolder()
        {
            string folder = Path.Combine(CurrentProjectRoot, "Assets", "ProjectInitializer", "Presets");
            Directory.CreateDirectory(folder);
            string ignorePath = Path.Combine(folder, ".gitignore");
            if (!File.Exists(ignorePath)) File.WriteAllText(ignorePath, "*\n!.gitignore\n");
            AssetDatabase.Refresh();
        }

        /// <summary>从另一个项目导入本地预设；新项目重新生成 GUID，不复制旧 .meta。</summary>
        public static List<string> ImportPresetsFromProject(string projectRoot)
        {
            projectRoot = Path.GetFullPath(projectRoot);
            if (!IsUnityProject(projectRoot)) throw new ArgumentException("不是有效的 Unity 项目。", nameof(projectRoot));
            EnsurePresetFolder();
            var imported = new List<string>();
            var folders = new[]
            {
                Path.Combine(projectRoot, "Assets", "ProjectInitializer", "Presets"),
                Path.Combine(projectRoot, "Packages", "ProjectInitializer", "Presets")
            };
            foreach (string folder in folders.Where(Directory.Exists))
            foreach (string source in Directory.GetFiles(folder, "*.asset", SearchOption.TopDirectoryOnly))
            {
                if (FindAllPresets().Any(p => string.Equals(p.importedFromPath, source, StringComparison.OrdinalIgnoreCase)))
                    continue;
                string name = Path.GetFileNameWithoutExtension(source);
                string existingPath = PresetFolder + "/" + name + ".asset";
                string existingFile = Path.Combine(CurrentProjectRoot, existingPath);
                if (File.Exists(existingFile) && File.ReadAllBytes(existingFile).SequenceEqual(File.ReadAllBytes(source)))
                    continue;
                string destination = AssetDatabase.GenerateUniqueAssetPath(existingPath);
                string destinationFile = Path.Combine(CurrentProjectRoot, destination);
                File.Copy(source, destinationFile);
                string archiveSource = Path.Combine(folder, name + ".plugins.bytes");
                string archivePath = destination.Substring(0, destination.Length - ".asset".Length) + ".plugins.bytes";
                if (File.Exists(archiveSource)) File.Copy(archiveSource, Path.Combine(CurrentProjectRoot, archivePath));
                AssetDatabase.ImportAsset(destination);
                if (File.Exists(archiveSource)) AssetDatabase.ImportAsset(archivePath);
                var preset = AssetDatabase.LoadAssetAtPath<ProjectInitPreset>(destination);
                if (preset == null)
                {
                    Debug.LogWarning($"[ProjectInitializer] 无法读取预设 {source}，请确认两项目使用同一版本的工具。");
                    AssetDatabase.DeleteAsset(destination);
                    if (File.Exists(Path.Combine(CurrentProjectRoot, archivePath))) AssetDatabase.DeleteAsset(archivePath);
                    continue;
                }
                preset.importedFromPath = source;
                preset.pluginArchive = File.Exists(archiveSource)
                    ? AssetDatabase.LoadAssetAtPath<TextAsset>(archivePath) : null;
                EditorUtility.SetDirty(preset);
                imported.Add(destination);
            }
            AssetDatabase.SaveAssets();
            return imported;
        }
    }
}
