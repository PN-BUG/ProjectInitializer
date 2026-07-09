#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectInitializer
{
    /// <summary>
    /// 项目初始化工具主窗口 — 选择预设 → 一键执行（创建目录 + 安装包 + 应用设置）。
    /// 完全独立，不依赖任何外部框架。
    /// </summary>
    public class ProjectInitializerWindow : EditorWindow
    {
        private const string DoNotShowKey = "ProjectInitializer.DoNotShow";
        private const string SessionOpenedKey = "ProjectInitializer.SessionOpened";

        [Serializable]
        private class ExecutionLog
        {
            public string message;
            public LogType type;

            public enum LogType { Info, Success, Warning, Error }

            public ExecutionLog(string message, LogType type = LogType.Info)
            {
                this.message = message;
                this.type = type;
            }
        }

        private List<ProjectInitPreset> _presets;
        private int _selectedPresetIndex;
        private ProjectInitPreset _selectedPreset;

        private Vector2 _logScroll;
        private Vector2 _overviewScroll;
        private readonly List<ExecutionLog> _executionLogs = new List<ExecutionLog>();

        private PackageInstaller _packageInstaller;
        private bool _isExecuting;
        private bool _pkgInstalled;

        // 执行选项
        private bool _optCreateDirectories = true;
        private bool _optInstallPackages = true;
        private bool _optApplySettings = true;

        [MenuItem("Tools/项目初始化工具", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<ProjectInitializerWindow>("项目初始化工具");
            window.minSize = new Vector2(560, 480);
            window.Show();
        }

        [InitializeOnLoad]
        private static class AutoOpen
        {
            static AutoOpen()
            {
                EditorApplication.delayCall += TryOpen;
            }

            private static void TryOpen()
            {
                if (EditorPrefs.GetBool(DoNotShowKey, false))
                    return;
                if (SessionState.GetBool(SessionOpenedKey, false))
                    return;
                SessionState.SetBool(SessionOpenedKey, true);
                ShowWindow();
            }
        }

        private void OnEnable()
        {
            _packageInstaller = new PackageInstaller();
            RefreshPresets();
        }

        private void OnDisable()
        {
            EditorPrefs.SetBool(DoNotShowKey, _doNotShowAgain);
        }

        private void RefreshPresets()
        {
            _presets = PresetManager.FindAllPresets();

            // 如果没有预设，创建一个默认预设并保存
            if (_presets.Count == 0)
            {
                PresetManager.EnsurePresetFolder();
                var defaultPreset = PresetManager.CreateDefaultPreset();
                PresetManager.SavePreset(defaultPreset);
                _presets = PresetManager.FindAllPresets();
            }

            if (_presets.Count > 0)
            {
                _selectedPresetIndex = Mathf.Clamp(_selectedPresetIndex, 0, _presets.Count - 1);
                _selectedPreset = _presets[_selectedPresetIndex];
                AutoUncheckExisting();
            }
            else
            {
                _selectedPreset = null;
            }
        }

        /// <summary>
        /// 自动取消勾选已存在的目录和已安装的包。
        /// </summary>
        private void AutoUncheckExisting()
        {
            if (_selectedPreset == null) return;

            // 目录：已存在则取消勾选
            if (_selectedPreset.directories != null)
            {
                foreach (var dir in _selectedPreset.directories)
                {
                    if (dir != null && dir.enabled && DirectoryTemplateCreator.DirectoryExists(dir.path))
                        dir.enabled = false;
                }
            }

            // 包：包安装检查是异步的，先标记需要检查
            _needsPackageCheck = _selectedPreset.packages != null && _selectedPreset.packages.Count > 0;
        }

        private bool _needsPackageCheck;

        private bool _doNotShowAgain;

        private void OnGUI()
        {
            // 延迟触发包安装状态检查
            if (_needsPackageCheck && !_isExecuting)
            {
                _needsPackageCheck = false;
                _packageInstaller.CheckPackages(_selectedPreset.packages, result =>
                {
                    if (result.success && _selectedPreset != null)
                    {
                        var installed = new HashSet<string>(result.installedPackages.Select(p => p.packageName));
                        foreach (var pkg in _selectedPreset.packages)
                        {
                            if (pkg != null && pkg.selected && installed.Contains(pkg.packageName))
                                pkg.selected = false;
                        }
                        Repaint();
                    }
                });
            }

            EditorGUILayout.Space(6);
            DrawTitle();
            EditorGUILayout.Space(4);

            if (_selectedPreset != null)
            {
                DrawSection("① 选择预设", ClrAccent);
                EditorGUILayout.Space(2);
                DrawPresetSelector();
                EditorGUILayout.Space(8);

                DrawSection("② 预设概览（可勾选）", new Color(0.35f, 0.70f, 0.75f));
                EditorGUILayout.Space(2);
                DrawPresetOverview();
                EditorGUILayout.Space(8);

                DrawSection("③ 执行选项", new Color(0.55f, 0.45f, 0.85f));
                EditorGUILayout.Space(2);
                DrawExecutionOptions();
                EditorGUILayout.Space(4);
                DrawExecuteButton();
                EditorGUILayout.Space(8);

                DrawSection("④ 执行日志", new Color(0.80f, 0.65f, 0.25f));
                EditorGUILayout.Space(2);
                DrawExecutionLog();
            }
            else
            {
                DrawNoPresetState();
            }

            EditorGUILayout.Space(6);
            DrawFooter();
        }

        #region Title

        // 功能区颜色
        private static readonly Color ClrAccent = new Color(0.30f, 0.55f, 0.95f);
        private static readonly Color ClrTeal = new Color(0.35f, 0.70f, 0.75f);
        private static readonly Color ClrPurple = new Color(0.55f, 0.45f, 0.85f);
        private static readonly Color ClrOrange = new Color(0.80f, 0.65f, 0.25f);
        private static readonly Color ClrDim = new Color(0.55f, 0.55f, 0.55f);

        /// <summary>
        /// 绘制功能区域标题 — 左侧色条 + 粗体标题。
        /// </summary>
        private static void DrawSection(string title, Color accent)
        {
            EditorGUILayout.BeginHorizontal();
            Rect bar = GUILayoutUtility.GetRect(3, 16, GUILayout.Width(3));
            EditorGUI.DrawRect(bar, accent);
            GUILayout.Space(6);
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            style.normal.textColor = Color.white;
            GUILayout.Label(title, style);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTitle()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("项目初始化工具", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            bool newDoNotShow = EditorGUILayout.ToggleLeft("不再自动弹出", _doNotShowAgain, GUILayout.Width(120));
            if (newDoNotShow != _doNotShowAgain)
            {
                _doNotShowAgain = newDoNotShow;
                EditorPrefs.SetBool(DoNotShowKey, _doNotShowAgain);
            }
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Preset Selector

        private void DrawPresetSelector()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("选择预设", EditorStyles.boldLabel);

            if (_presets == null || _presets.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到任何预设。点击下方按钮创建。", MessageType.Warning);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("创建默认预设", GUILayout.Height(26)))
                {
                    PresetManager.EnsurePresetFolder();
                    var preset = PresetManager.CreateDefaultPreset();
                    PresetManager.SavePreset(preset);
                    RefreshPresets();
                }
                if (GUILayout.Button("打开预设编辑器", GUILayout.Height(26)))
                {
                    PresetEditorWindow.ShowWindow();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();

                string[] presetNames = _presets.Select(p => p.presetName).ToArray();
                EditorGUI.BeginChangeCheck();
                _selectedPresetIndex = EditorGUILayout.Popup("预设", _selectedPresetIndex, presetNames);
                if (EditorGUI.EndChangeCheck())
                {
                    _selectedPreset = _presets[_selectedPresetIndex];
                    AutoUncheckExisting();
                }
                EditorGUILayout.EndHorizontal();

                if (_selectedPreset != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(_selectedPreset.description, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("编辑当前预设", GUILayout.Height(24)))
                {
                    PresetEditorWindow.ShowWindow(_selectedPreset, false);
                }
                if (GUILayout.Button("新建预设", GUILayout.Height(24)))
                {
                    PresetEditorWindow.ShowWindow(PresetManager.CreateEmptyPreset(), true);
                }
                if (GUILayout.Button("刷新列表", GUILayout.Height(24)))
                {
                    RefreshPresets();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        // 折叠状态
        private bool _dirFoldout = true;
        private bool _pkgFoldout = true;
        private bool _setFoldout = true;

        private void DrawPresetOverview()
        {
            EditorGUILayout.BeginVertical("box");

            _overviewScroll = EditorGUILayout.BeginScrollView(_overviewScroll, GUILayout.MaxHeight(300));

            DrawDirectoryOverviewTree();
            EditorGUILayout.Space(4);
            DrawPackageOverview();
            EditorGUILayout.Space(4);
            DrawSettingsOverview();
        }

        #region Directory Overview — 分层树

        private void DrawDirectoryOverview()
        {
            EditorGUILayout.BeginHorizontal();
            _dirFoldout = EditorGUILayout.Foldout(_dirFoldout,
                $"📁 目录模板 ({_selectedPreset.EnabledDirectoryCount}/{_selectedPreset.directories.Count})", true);
            if (_dirFoldout && _selectedPreset.directories.Count > 0)
            {
                if (GUILayout.Button("全选", GUILayout.Width(40))) { foreach (var d in _selectedPreset.directories) d.enabled = true; MarkDirty(); }
                if (GUILayout.Button("全不选", GUILayout.Width(50))) { foreach (var d in _selectedPreset.directories) d.enabled = false; MarkDirty(); }
            }
            EditorGUILayout.EndHorizontal();

            if (!_dirFoldout) return;

            if (_selectedPreset.directories.Count == 0)
            {
                EditorGUILayout.LabelField("  (无)", EditorStyles.miniLabel);
                return;
            }

            // 构建分层树
            var root = BuildDirectoryTree(_selectedPreset.directories);
            DrawDirTreeNode(root, 0);
        }

        private class DirNode
        {
            public string name;
            public DirectoryEntry entry; // 非null 表示这是一个叶子节点（即用户配置的路径）
            public List<DirNode> children = new List<DirNode>();
        }

        private static DirNode BuildDirectoryTree(List<DirectoryEntry> entries)
        {
            var root = new DirNode { name = "Assets" };
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.path)) continue;
                var parts = entry.path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                var current = root;
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var child = current.children.FirstOrDefault(c => c.name == part);
                    if (child == null)
                    {
                        child = new DirNode { name = part };
                        current.children.Add(child);
                    }
                    if (i == parts.Length - 1)
                        child.entry = entry; // 最后一段关联用户条目
                    current = child;
                }
            }
            return root;
        }

        private void DrawDirTreeNode(DirNode node, int depth)
        {
            if (depth > 0) // 跳过 root "Assets"
            {
                EditorGUI.indentLevel = depth;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(depth * 14);

                if (node.entry != null)
                {
                    // 叶子节点 — 可勾选
                    bool exists = DirectoryTemplateCreator.DirectoryExists(node.entry.path);
                    EditorGUI.BeginChangeCheck();
                    node.entry.enabled = EditorGUILayout.Toggle(node.entry.enabled, GUILayout.Width(16));
                    if (EditorGUI.EndChangeCheck()) MarkDirty();
                    EditorGUILayout.LabelField(node.name, EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    var c = GUI.color;
                    GUI.color = exists ? new Color(0.5f, 0.8f, 0.5f) : new Color(0.7f, 0.7f, 0.7f);
                    GUILayout.Label(exists ? "✓" : "○", EditorStyles.miniLabel, GUILayout.Width(20));
                    GUI.color = c;
                }
                else
                {
                    // 分组节点 — 显示文件夹图标和名称
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField($"📁 {node.name}", EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.EndHorizontal();
            }

            foreach (var child in node.children)
                DrawDirTreeNode(child, depth + 1);

            EditorGUI.indentLevel = 0;
        }

        private void DrawDirectoryOverviewTree()
        {
            // 用简洁版替代，在概览中按层级显示
            EditorGUILayout.BeginHorizontal();
            _dirFoldout = EditorGUILayout.Foldout(_dirFoldout,
                $"📁 目录模板 ({_selectedPreset.EnabledDirectoryCount}/{_selectedPreset.directories.Count})", true);
            if (_dirFoldout && _selectedPreset.directories.Count > 0)
            {
                if (GUILayout.Button("全选", GUILayout.Width(40))) { foreach (var d in _selectedPreset.directories) d.enabled = true; MarkDirty(); }
                if (GUILayout.Button("全不选", GUILayout.Width(50))) { foreach (var d in _selectedPreset.directories) d.enabled = false; MarkDirty(); }
            }
            EditorGUILayout.EndHorizontal();

            if (!_dirFoldout) return;
            if (_selectedPreset.directories.Count == 0)
            {
                EditorGUILayout.LabelField("  (无)", EditorStyles.miniLabel);
                return;
            }

            // 构建并绘制分层树
            var root = BuildDirectoryTree(_selectedPreset.directories);
            DrawDirNodeTree(root, 0);
        }

        private void DrawDirNodeTree(DirNode node, int depth)
        {
            if (depth > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(depth * 16);

                if (node.entry != null)
                {
                    // 叶子节点 — 可勾选
                    bool exists = DirectoryTemplateCreator.DirectoryExists(node.entry.path);
                    EditorGUI.BeginChangeCheck();
                    node.entry.enabled = EditorGUILayout.Toggle(node.entry.enabled, GUILayout.Width(16));
                    if (EditorGUI.EndChangeCheck()) MarkDirty();
                    EditorGUILayout.LabelField(node.name, EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    var c = GUI.color;
                    GUI.color = exists ? new Color(0.5f, 0.8f, 0.5f) : new Color(0.7f, 0.7f, 0.7f);
                    GUILayout.Label(exists ? "已存在" : "未创建", EditorStyles.miniLabel, GUILayout.Width(45));
                    GUI.color = c;
                }
                else
                {
                    // 分组节点 — 只显示名称
                    EditorGUILayout.LabelField($"📂 {node.name}", EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.EndHorizontal();
            }

            foreach (var child in node.children)
                DrawDirNodeTree(child, depth + 1);
        }

        #endregion

        #region Preset Overview — Packages & Settings

        // ── 包分组定义 ──
        private static readonly string[] UnityOfficialPrefixes =
        {
            "com.unity.",
        };

        private static readonly (string prefix, string label)[] PackageGroupRules =
        {
            ("com.unity.",         "📦 Unity 官方"),
            ("com.cysharp.",       "📦 第三方"),
            ("com.akbiggs.",       "📦 第三方"),
            ("com.cysharp.unitask","📦 第三方"),
            ("com.unityframework.","🏠 UnityFramework"),
            ("com.zko.",           "🏠 UnityFramework"),
        };

        private void DrawPackageGroups(HashSet<string> installed)
        {
            // 分类
            var groups = new List<(string label, List<PackageEntry> items)>();
            var matched = new HashSet<int>();

            for (int g = 0; g < PackageGroupRules.Length; g++)
            {
                var rule = PackageGroupRules[g];
                var items = new List<PackageEntry>();

                for (int i = 0; i < _selectedPreset.packages.Count; i++)
                {
                    if (matched.Contains(i)) continue;
                    var pkg = _selectedPreset.packages[i];
                    if (pkg == null) continue;

                    bool belongs = false;
                    if (rule.prefix == "com.unity." && pkg.packageName.StartsWith("com.unity."))
                    {
                        // com.unityframework. 不归入 Unity 官方
                        belongs = !pkg.packageName.StartsWith("com.unityframework.");
                    }
                    else if (pkg.packageName.StartsWith(rule.prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        belongs = true;
                    }

                    if (belongs)
                    {
                        items.Add(pkg);
                        matched.Add(i);
                    }
                }

                if (items.Count > 0)
                    groups.Add((rule.label, items));
            }

            // 未匹配的包 → "其他"
            var others = new List<PackageEntry>();
            for (int i = 0; i < _selectedPreset.packages.Count; i++)
            {
                if (!matched.Contains(i))
                {
                    var pkg = _selectedPreset.packages[i];
                    if (pkg != null) others.Add(pkg);
                }
            }
            if (others.Count > 0)
                groups.Add(("📦 其他", others));

            // 绘制分组
            foreach (var (label, items) in groups)
            {
                int selectedCount = items.Count(p => p.selected);

                // 分组标题
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(8);
                EditorGUILayout.LabelField($"{label} ({selectedCount}/{items.Count})", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                // UnityFramework 子层级
                bool isFramework = label.Contains("UnityFramework");
                bool isFirst = true;

                EditorGUI.indentLevel++;
                foreach (var pkg in items)
                {
                    bool isInstalled = installed != null && installed.Contains(pkg.packageName);
                    bool isChild = isFramework && pkg.packageName != "com.unityframework.core";

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(isChild ? 28 : 16);

                    if (isFramework && isFirst && pkg.packageName == "com.unityframework.core")
                    {
                        // Core 是根节点
                    }
                    else if (isFramework && isChild)
                    {
                        EditorGUILayout.LabelField(isFirst ? "├" : "├", EditorStyles.miniLabel, GUILayout.Width(12));
                    }

                    EditorGUI.BeginChangeCheck();
                    pkg.selected = EditorGUILayout.Toggle(pkg.selected, GUILayout.Width(16));
                    if (EditorGUI.EndChangeCheck()) MarkDirty();

                    EditorGUILayout.LabelField(pkg.displayName, EditorStyles.miniLabel, GUILayout.Width(120));
                    EditorGUILayout.LabelField(pkg.packageName, EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();

                    var c = GUI.color;
                    GUI.color = isInstalled ? new Color(0.5f, 0.8f, 0.5f) : new Color(0.8f, 0.7f, 0.4f);
                    GUILayout.Label(isInstalled ? "已安装" : "未安装", EditorStyles.miniLabel, GUILayout.Width(45));
                    GUI.color = c;

                    EditorGUILayout.EndHorizontal();
                    isFirst = false;
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }
        }

        private void DrawPackageOverview()
        {
            // ── 依赖包 ──
            EditorGUILayout.BeginHorizontal();
            _pkgFoldout = EditorGUILayout.Foldout(_pkgFoldout, $"📦 依赖包 ({_selectedPreset.SelectedPackageCount}/{_selectedPreset.packages.Count})", true);
            if (_pkgFoldout && _selectedPreset.packages.Count > 0)
            {
                if (GUILayout.Button("全选", GUILayout.Width(40))) { foreach (var p in _selectedPreset.packages) p.selected = true; MarkDirty(); }
                if (GUILayout.Button("全不选", GUILayout.Width(50))) { foreach (var p in _selectedPreset.packages) p.selected = false; MarkDirty(); }
            }
            EditorGUILayout.EndHorizontal();

            if (_pkgFoldout)
            {
                if (_selectedPreset.packages.Count == 0)
                {
                    EditorGUILayout.LabelField("  (无)", EditorStyles.miniLabel);
                }
                else
                {
                    var installed = _packageInstaller.GetInstalledPackageNames();
                    DrawPackageGroups(installed);
                }
            }
        }

        private void DrawSettingsOverview()
        {
            EditorGUILayout.Space(4);

            // ── 项目设置 ──
            EditorGUILayout.BeginHorizontal();
            _setFoldout = EditorGUILayout.Foldout(_setFoldout, $"⚙ 项目设置 ({_selectedPreset.EnabledSettingsCount}/{_selectedPreset.settings.Count})", true);
            if (_setFoldout && _selectedPreset.settings.Count > 0)
            {
                if (GUILayout.Button("全选", GUILayout.Width(40))) { foreach (var s in _selectedPreset.settings) s.enabled = true; MarkDirty(); }
                if (GUILayout.Button("全不选", GUILayout.Width(50))) { foreach (var s in _selectedPreset.settings) s.enabled = false; MarkDirty(); }
            }
            EditorGUILayout.EndHorizontal();

            if (_setFoldout)
            {
                if (_selectedPreset.settings.Count == 0)
                {
                    EditorGUILayout.LabelField("  (无)", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < _selectedPreset.settings.Count; i++)
                    {
                        var setting = _selectedPreset.settings[i];
                        if (setting == null) continue;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUI.BeginChangeCheck();
                        setting.enabled = EditorGUILayout.Toggle(setting.enabled, GUILayout.Width(16));
                        if (EditorGUI.EndChangeCheck()) MarkDirty();
                        EditorGUILayout.LabelField($"[{setting.category}] {setting.key} = {setting.value}", EditorStyles.miniLabel);
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void MarkDirty()
        {
            if (_selectedPreset != null)
                EditorUtility.SetDirty(_selectedPreset);
        }

        #endregion

        #region Execution

        private void DrawExecutionOptions()
        {
            EditorGUILayout.BeginVertical("box");
            _optCreateDirectories = EditorGUILayout.ToggleLeft("📁 创建目录模板", _optCreateDirectories);
            _optInstallPackages = EditorGUILayout.ToggleLeft("📦 安装依赖包", _optInstallPackages);
            _optApplySettings = EditorGUILayout.ToggleLeft("⚙ 应用项目设置", _optApplySettings);
            EditorGUILayout.EndVertical();
        }

        private void DrawExecuteButton()
        {
            bool canExecute = !_isExecuting && (_optCreateDirectories || _optInstallPackages || _optApplySettings);

            EditorGUI.BeginDisabledGroup(!canExecute);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⚡ 一键初始化", GUILayout.Height(36)))
            {
                StartExecution();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            if (_isExecuting)
            {
                EditorGUILayout.LabelField("正在执行...", EditorStyles.boldLabel);
            }
        }

        private void StartExecution()
        {
            _executionLogs.Clear();
            _isExecuting = true;
            _pkgInstalled = false;

            Log("开始执行项目初始化...", ExecutionLog.LogType.Info);

            // Step 1: 创建目录
            if (_optCreateDirectories)
            {
                Log("─ 创建目录模板 ─", ExecutionLog.LogType.Info);
                var result = DirectoryTemplateCreator.Create(_selectedPreset.directories);
                Log(result.ToString(), ExecutionLog.LogType.Success);
                foreach (var path in result.createdPaths)
                    Log($"  + {path}", ExecutionLog.LogType.Success);
                foreach (var path in result.skippedPaths)
                    Log($"  ~ {path} (已存在)", ExecutionLog.LogType.Warning);
            }

            // Step 2: 安装包
            if (_optInstallPackages && _selectedPreset.SelectedPackageCount > 0)
            {
                Log("─ 安装依赖包 ─", ExecutionLog.LogType.Info);
                var packagesToInstall = _selectedPreset.packages.Where(p => p != null && p.selected).ToList();
                _packageInstaller.InstallPackages(packagesToInstall, OnPackagesInstalled);
            }
            else
            {
                if (_optInstallPackages)
                    Log("无需要安装的依赖包。", ExecutionLog.LogType.Info);
                _pkgInstalled = true;
            }

            // Step 3: 应用设置
            if (_optApplySettings)
            {
                Log("─ 应用项目设置 ─", ExecutionLog.LogType.Info);
                var result = ProjectSettingsApplier.Apply(_selectedPreset.settings);
                Log(result.ToString(), ExecutionLog.LogType.Success);
                foreach (var s in result.appliedSettings)
                    Log($"  ✓ {s}", ExecutionLog.LogType.Success);
                foreach (var s in result.failedSettings)
                    Log($"  ✗ {s}", ExecutionLog.LogType.Error);
            }

            CheckExecutionComplete();
        }

        private void OnPackagesInstalled()
        {
            Log(_packageInstaller.StatusMessage,
                _packageInstaller.FailedCount > 0 ? ExecutionLog.LogType.Warning : ExecutionLog.LogType.Success);

            foreach (var failed in _packageInstaller.FailedPackages)
                Log($"  ✗ 失败: {failed}", ExecutionLog.LogType.Error);

            _pkgInstalled = true;
            CheckExecutionComplete();
        }

        private void CheckExecutionComplete()
        {
            // 如果包安装是异步的，等待它完成
            if (_optInstallPackages && _selectedPreset.SelectedPackageCount > 0 && !_pkgInstalled)
                return;

            _isExecuting = false;
            Log("═ 执行完成 ═", ExecutionLog.LogType.Success);
            Repaint();
        }

        private void DrawExecutionLog()
        {
            if (_executionLogs.Count == 0)
                return;

            EditorGUILayout.BeginVertical("box");

            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(80), GUILayout.MaxHeight(180));

            foreach (var log in _executionLogs)
            {
                Color oldColor = GUI.color;
                switch (log.type)
                {
                    case ExecutionLog.LogType.Success:
                        GUI.color = new Color(0.6f, 0.8f, 0.6f);
                        break;
                    case ExecutionLog.LogType.Warning:
                        GUI.color = new Color(0.9f, 0.8f, 0.4f);
                        break;
                    case ExecutionLog.LogType.Error:
                        GUI.color = new Color(0.9f, 0.5f, 0.5f);
                        break;
                    default:
                        GUI.color = GUI.color;
                        break;
                }
                EditorGUILayout.LabelField(log.message, EditorStyles.miniLabel);
                GUI.color = oldColor;
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("清空日志", GUILayout.Height(20)))
                _executionLogs.Clear();

            EditorGUILayout.EndVertical();
        }

        private void Log(string message, ExecutionLog.LogType type = ExecutionLog.LogType.Info)
        {
            _executionLogs.Add(new ExecutionLog(message, type));
            Repaint();
        }

        #endregion

        #region No Preset State

        private void DrawNoPresetState()
        {
            EditorGUILayout.HelpBox("未加载任何预设。请创建或选择一个预设。", MessageType.Warning);
            EditorGUILayout.Space(4);
            if (GUILayout.Button("创建默认预设", GUILayout.Height(30)))
            {
                PresetManager.EnsurePresetFolder();
                var preset = PresetManager.CreateDefaultPreset();
                PresetManager.SavePreset(preset);
                RefreshPresets();
            }
        }

        #endregion

        #region Footer

        private void DrawFooter()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Project Initializer v1.0  ·  通用项目初始化工具", EditorStyles.miniLabel);
        }

        #endregion
    }
}
#endif
