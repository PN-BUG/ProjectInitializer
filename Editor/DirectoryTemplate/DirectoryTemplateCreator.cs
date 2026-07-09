using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectInitializer
{
    /// <summary>
    /// 目录模板创建器 — 根据预设中的目录条目在 Assets 下创建文件夹。
    /// </summary>
    public static class DirectoryTemplateCreator
    {
        public struct CreateResult
        {
            public int createdCount;
            public int skippedCount;
            public List<string> createdPaths;
            public List<string> skippedPaths;

            public override string ToString()
            {
                return $"已创建 {createdCount} 个目录" + (skippedCount > 0 ? $"，跳过 {skippedCount} 个已存在的目录" : "");
            }
        }

        /// <summary>
        /// 执行目录创建。返回创建结果。
        /// </summary>
        public static CreateResult Create(List<DirectoryEntry> entries)
        {
            var result = new CreateResult
            {
                createdPaths = new List<string>(),
                skippedPaths = new List<string>()
            };

            if (entries == null || entries.Count == 0)
                return result;

            string assetsPath = Application.dataPath;

            foreach (var entry in entries)
            {
                if (entry == null || !entry.enabled)
                    continue;

                string cleanPath = SanitizePath(entry.path);
                if (string.IsNullOrEmpty(cleanPath))
                    continue;

                string fullPath = Path.Combine(assetsPath, cleanPath);
                fullPath = fullPath.Replace('\\', Path.DirectorySeparatorChar);

                if (Directory.Exists(fullPath))
                {
                    result.skippedCount++;
                    result.skippedPaths.Add($"Assets/{cleanPath}");
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(fullPath);
                    result.createdCount++;
                    result.createdPaths.Add($"Assets/{cleanPath}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ProjectInitializer] 创建目录失败: Assets/{cleanPath}\n{e.Message}");
                    result.skippedCount++;
                    result.skippedPaths.Add($"Assets/{cleanPath} (创建失败)");
                }
            }

            AssetDatabase.Refresh();
            return result;
        }

        /// <summary>
        /// 清理路径中的非法字符和前导斜杠。
        /// </summary>
        private static string SanitizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            path = path.Trim().Replace('\\', '/');

            // 移除前导 Assets/ 前缀
            if (path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                path = path.Substring(7);
            if (path.StartsWith("/"))
                path = path.TrimStart('/');

            // 过滤空段
            var parts = path.Split('/');
            var cleanParts = new System.Collections.Generic.List<string>();
            foreach (var part in parts)
            {
                string clean = part.Trim();
                if (!string.IsNullOrEmpty(clean))
                    cleanParts.Add(clean);
            }

            return string.Join("/", cleanParts);
        }

        /// <summary>
        /// 检查目录是否已存在。
        /// </summary>
        public static bool DirectoryExists(string relativePath)
        {
            string cleanPath = SanitizePath(relativePath);
            if (string.IsNullOrEmpty(cleanPath))
                return false;
            string fullPath = Path.Combine(Application.dataPath, cleanPath);
            return Directory.Exists(fullPath);
        }
    }
}
