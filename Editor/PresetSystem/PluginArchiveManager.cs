using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectInitializer
{
    /// <summary>将插件文件和 .meta 打包到预设旁的 .bytes 文件，并按勾选项复制。</summary>
    public static class PluginArchiveManager
    {
        public struct CopyResult
        {
            public readonly List<string> copied;
            public readonly List<string> skipped;
            public readonly List<string> failed;

            public CopyResult(bool initialize)
            {
                copied = new List<string>();
                skipped = new List<string>();
                failed = new List<string>();
            }
        }

        public static bool IsValidPluginPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Plugins/", StringComparison.Ordinal)) return false;
            if (path.IndexOf('\\') >= 0 || path.IndexOf(':') >= 0) return false;
            return path.Split('/').All(part => part.Length > 0 && part != "." && part != ".." &&
                                                part.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
        }

        public static bool PluginExists(PluginEntry entry)
        {
            if (entry == null || !IsValidPluginPath(entry.path)) return false;
            string fullPath = Path.Combine(Application.dataPath, entry.path);
            return entry.isDirectory
                ? Directory.Exists(fullPath) && Directory.EnumerateFileSystemEntries(fullPath).Any()
                : File.Exists(fullPath);
        }

        /// <summary>保存预设时生成随行归档。所有条目都被打包，应用时才按勾选复制。</summary>
        public static bool RefreshArchive(ProjectInitPreset preset)
        {
            if (preset == null || preset.plugins == null || preset.plugins.Count == 0) return true;
            string presetPath = AssetDatabase.GetAssetPath(preset);
            if (string.IsNullOrEmpty(presetPath) || !presetPath.StartsWith("Assets/", StringComparison.Ordinal)) return false;
            string archivePath = presetPath.Substring(0, presetPath.Length - ".asset".Length) + ".plugins.bytes";
            string archiveFile = Path.GetFullPath(archivePath);
            string sourceAssetsPath = Path.Combine(preset.sourceProjectRoot ?? PresetManager.CurrentProjectRoot, "Assets");

            // 导入的预设保留原归档，避免误用当前项目同名插件覆盖它。
            if (preset.sourceProjectRoot == null && preset.pluginArchive != null)
            {
                string existing = Path.GetFullPath(AssetDatabase.GetAssetPath(preset.pluginArchive));
                if (!string.Equals(existing, archiveFile, StringComparison.OrdinalIgnoreCase))
                    File.Copy(existing, archiveFile, true);
                AssetDatabase.ImportAsset(archivePath);
                preset.pluginArchive = AssetDatabase.LoadAssetAtPath<TextAsset>(archivePath);
                EditorUtility.SetDirty(preset);
                return preset.pluginArchive != null;
            }

            foreach (var plugin in preset.plugins)
            {
                if (plugin == null || !IsValidPluginPath(plugin.path) ||
                    !(plugin.isDirectory
                        ? Directory.Exists(Path.Combine(sourceAssetsPath, plugin.path))
                        : File.Exists(Path.Combine(sourceAssetsPath, plugin.path))))
                {
                    // 在另一个项目里另存预设时，保留原有归档。
                    if (preset.pluginArchive == null) return false;
                    string existing = Path.GetFullPath(AssetDatabase.GetAssetPath(preset.pluginArchive));
                    if (!string.Equals(existing, archiveFile, StringComparison.OrdinalIgnoreCase))
                        File.Copy(existing, archiveFile, true);
                    AssetDatabase.ImportAsset(archivePath);
                    preset.pluginArchive = AssetDatabase.LoadAssetAtPath<TextAsset>(archivePath);
                    EditorUtility.SetDirty(preset);
                    return true;
                }
            }

            string temp = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    var added = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var plugin in preset.plugins)
                    {
                        string fullPath = Path.Combine(sourceAssetsPath, plugin.path);
                        if (plugin.isDirectory)
                        {
                            foreach (string dir in new[] { fullPath }.Concat(Directory.GetDirectories(fullPath, "*", SearchOption.AllDirectories)))
                            {
                                string relative = RelativeToAssets(dir, sourceAssetsPath) + "/";
                                if (added.Add(relative)) archive.CreateEntry(relative);
                            }
                            foreach (string file in Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories))
                                AddFile(archive, file, added, sourceAssetsPath);
                        }
                        else AddFile(archive, fullPath, added, sourceAssetsPath);

                        string meta = fullPath + ".meta";
                        if (File.Exists(meta)) AddFile(archive, meta, added, sourceAssetsPath);
                    }
                }
                File.Copy(temp, archiveFile, true);
                AssetDatabase.ImportAsset(archivePath);
                preset.pluginArchive = AssetDatabase.LoadAssetAtPath<TextAsset>(archivePath);
                EditorUtility.SetDirty(preset);
                return preset.pluginArchive != null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProjectInitializer] 插件归档失败: {e.Message}");
                return false;
            }
            finally { File.Delete(temp); }
        }

        public static CopyResult CopySelected(ProjectInitPreset preset)
        {
            var result = new CopyResult(true);
            if (preset?.plugins == null || preset.SelectedPluginCount == 0) return result;
            if (preset.pluginArchive == null)
            {
                foreach (var plugin in preset.plugins.Where(p => p != null && p.copyFiles))
                    result.failed.Add(plugin.path + " (缺少插件归档)");
                return result;
            }

            try
            {
                using (var stream = new MemoryStream(preset.pluginArchive.bytes, false))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var plugin in preset.plugins.Where(p => p != null && p.copyFiles))
                    {
                        if (!IsValidPluginPath(plugin.path)) { result.failed.Add(plugin.path); continue; }
                        if (PluginExists(plugin)) { result.skipped.Add(plugin.path); continue; }

                        string prefix = plugin.path + "/";
                        var entries = archive.Entries.Where(e => e.FullName == plugin.path ||
                            e.FullName == plugin.path + ".meta" || e.FullName.StartsWith(prefix, StringComparison.Ordinal)).ToList();
                        if (entries.Count == 0 || entries.Any(e => !IsSafeArchivePath(e.FullName)))
                        { result.failed.Add(plugin.path + " (归档内容无效)"); continue; }

                        try
                        {
                            foreach (var entry in entries)
                            {
                                string target = Path.Combine(Application.dataPath, entry.FullName);
                                if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                                    Directory.CreateDirectory(target);
                                else
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                                    if (File.Exists(target)) continue;
                                    using (var input = entry.Open())
                                    using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write))
                                        input.CopyTo(output);
                                }
                            }
                            result.copied.Add(plugin.path);
                        }
                        catch (Exception e) { result.failed.Add(plugin.path + " (" + e.Message + ")"); }
                    }
                }
            }
            catch (Exception e)
            {
                result.failed.Add("无法读取插件归档: " + e.Message);
            }
            if (result.copied.Count > 0) AssetDatabase.Refresh();
            return result;
        }

        private static string RelativeToAssets(string path, string assetsPath) =>
            Path.GetRelativePath(assetsPath, path).Replace('\\', '/');

        private static void AddFile(ZipArchive archive, string file, HashSet<string> added, string assetsPath)
        {
            string relative = RelativeToAssets(file, assetsPath);
            if (!added.Add(relative)) return;
            var entry = archive.CreateEntry(relative, System.IO.Compression.CompressionLevel.Optimal);
            using (var input = File.OpenRead(file))
            using (var output = entry.Open()) input.CopyTo(output);
        }

        private static bool IsSafeArchivePath(string path)
        {
            string clean = path.EndsWith("/", StringComparison.Ordinal) ? path.TrimEnd('/') : path;
            return IsValidPluginPath(clean);
        }
    }
}
