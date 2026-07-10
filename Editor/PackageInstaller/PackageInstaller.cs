using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ProjectInitializer
{
    /// <summary>
    /// UPM 包安装器 — 检查缺失包并异步安装。
    /// </summary>
    public class PackageInstaller
    {
        public enum InstallState { Idle, Checking, Ready, Installing, Completed, Failed }

        public struct CheckResult
        {
            public List<PackageEntry> missingPackages;
            public List<PackageEntry> installedPackages;
            public bool success;
            public string errorMessage;
        }

        public InstallState State { get; private set; } = InstallState.Idle;
        public string StatusMessage { get; private set; } = string.Empty;
        public int InstalledCount { get; private set; }
        public int FailedCount { get; private set; }
        public List<string> FailedPackages { get; } = new List<string>();

        private ListRequest _listRequest;
        private AddRequest _addRequest;
        private readonly Queue<PackageEntry> _installQueue = new Queue<PackageEntry>();
        private System.Action<CheckResult> _checkCallback;
        private System.Action _installCompleteCallback;
        private HashSet<string> _installedPackageNames;
        private List<PackageEntry> _packagesToCheck;

        /// <summary>
        /// 检查哪些包未安装。异步操作，完成后回调。
        /// </summary>
        public void CheckPackages(List<PackageEntry> packages, System.Action<CheckResult> onComplete)
        {
            _checkCallback = onComplete;
            _packagesToCheck = packages;
            State = InstallState.Checking;
            StatusMessage = "正在读取当前依赖包列表...";
            _listRequest = Client.List(true);
            EditorApplication.update += CheckListRequest;
        }

        private void CheckListRequest()
        {
            if (_listRequest == null || !_listRequest.IsCompleted)
                return;

            EditorApplication.update -= CheckListRequest;

            var result = new CheckResult
            {
                missingPackages = new List<PackageEntry>(),
                installedPackages = new List<PackageEntry>(),
                success = false
            };

            if (_listRequest.Status == StatusCode.Success)
            {
                _installedPackageNames = new HashSet<string>(_listRequest.Result.Select(p => p.name));
                result.success = true;
                StatusMessage = string.Empty;

                if (_packagesToCheck != null)
                {
                    foreach (var pkg in _packagesToCheck)
                    {
                        if (_installedPackageNames.Contains(pkg.packageName))
                            result.installedPackages.Add(pkg);
                        else
                            result.missingPackages.Add(pkg);
                    }
                }
            }
            else
            {
                result.errorMessage = _listRequest.Error?.message ?? "未知错误";
                StatusMessage = $"读取依赖包失败：{result.errorMessage}";
            }

            State = InstallState.Ready;
            _checkCallback?.Invoke(result);
        }

        /// <summary>
        /// 安装选中的包。异步操作，完成后回调。
        /// 安装前自动确保 com.zko.nodin 已写入 manifest.json。
        /// </summary>
        public void InstallPackages(List<PackageEntry> packages, System.Action onComplete)
        {
            // 确保 Nodin 依赖已就位
            EnsureNodinInManifest();

            _installCompleteCallback = onComplete;
            _installQueue.Clear();
            InstalledCount = 0;
            FailedCount = 0;
            FailedPackages.Clear();

            if (packages == null)
            {
                State = InstallState.Completed;
                _installCompleteCallback?.Invoke();
                return;
            }

            foreach (var pkg in packages)
            {
                if (pkg != null && pkg.selected)
                    _installQueue.Enqueue(pkg);
            }

            if (_installQueue.Count == 0)
            {
                State = InstallState.Completed;
                _installCompleteCallback?.Invoke();
                return;
            }

            State = InstallState.Installing;
            StatusMessage = "开始安装依赖包...";
            EditorApplication.update += InstallNextDependency;
        }

        private void InstallNextDependency()
        {
            if (_addRequest != null)
            {
                if (!_addRequest.IsCompleted)
                    return;

                if (_addRequest.Status == StatusCode.Failure)
                {
                    var failedItem = _installQueue.Count > 0 ? _installQueue.Peek() : null;
                    FailedCount++;
                    FailedPackages.Add(failedItem?.packageName ?? "unknown");
                    StatusMessage = $"安装失败：{_addRequest.Error.message}";
                    _addRequest = null;
                    // 不清空队列继续安装其他包，仅跳过当前失败的
                }
                else
                {
                    InstalledCount++;
                    _addRequest = null;
                }
            }

            if (_installQueue.Count == 0)
            {
                State = FailedCount > 0 ? InstallState.Failed : InstallState.Completed;
                StatusMessage = FailedCount > 0
                    ? $"安装完成：成功 {InstalledCount} 个，失败 {FailedCount} 个。"
                    : $"依赖包安装完成，共 {InstalledCount} 个。";
                EditorApplication.update -= InstallNextDependency;
                _installCompleteCallback?.Invoke();
                return;
            }

            var item = _installQueue.Dequeue();
            StatusMessage = $"正在安装：{item.displayName} ({item.packageName})";
            _addRequest = Client.Add(item.installSpec);
        }

        /// <summary>
        /// 获取当前已安装的包名集合（CheckPackages 成功后可用）。
        /// </summary>
        public HashSet<string> GetInstalledPackageNames()
        {
            return _installedPackageNames;
        }

        private const string NodinPackageName = "com.zko.nodin";
        private const string NodinGitUrl = "https://github.com/PN-BUG/Nodin.git";

        /// <summary>
        /// 确保 manifest.json 中包含 com.zko.nodin 依赖。
        /// Nodin 是 ProjectInitializer 和 UnityToolsHub 的核心依赖，
        /// 嵌入式包的 package.json 依赖不会被 UPM 自动解析，必须在 manifest.json 中声明。
        /// </summary>
        public static bool EnsureNodinInManifest()
        {
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("[ProjectInitializer] 未找到 manifest.json");
                return false;
            }

            string content = File.ReadAllText(manifestPath);

            // 精确匹配 JSON key："com.zko.nodin"（避免误匹配子字符串）
            if (Regex.IsMatch(content, $"\"{Regex.Escape(NodinPackageName)}\"\\s*:"))
                return true;

            // 在 dependencies 的第一个条目前插入 com.zko.nodin
            // 匹配 "dependencies": { 后的第一个引号
            var match = Regex.Match(content, @"(""dependencies""\s*:\s*\{\s*\r?\n\s*)(""[^""]+"")");
            if (!match.Success)
            {
                Debug.LogWarning("[ProjectInitializer] 无法解析 manifest.json 的 dependencies 块");
                return false;
            }

            string insert = $"{match.Groups[1].Value}\"{NodinPackageName}\": \"{NodinGitUrl}\",\n    {match.Groups[2].Value}";
            content = content.Substring(0, match.Index) + insert + content.Substring(match.Index + match.Length);

            File.WriteAllText(manifestPath, content);
            Debug.Log($"[ProjectInitializer] 已将 {NodinPackageName} 添加到 manifest.json");
            return true;
        }
    }
}
