#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectInitializer
{
    /// <summary>
    /// 预设编辑器窗口 — 可视化编辑预设的目录模板、依赖包、项目设置。
    /// 四 Tab 界面，支持拖拽识别、读取当前项目、保存为 .asset 文件。
    /// </summary>
    public class PresetEditorWindow : EditorWindow
    {
        private enum Tab { Directories, Packages, Plugins, Settings }

        private ProjectInitPreset _preset;
        private bool _isNewPreset;
        private Tab _currentTab = Tab.Directories;

        // Scroll
        private Vector2 _dirScroll, _pkgScroll, _pluginScroll, _setScroll, _descScroll;
        private readonly PresetRangeToggle _dirRangeToggle = new PresetRangeToggle();
        private readonly PresetRangeToggle _pkgRangeToggle = new PresetRangeToggle();
        private readonly PresetRangeToggle _pluginRangeToggle = new PresetRangeToggle();
        private readonly PresetRangeToggle _setRangeToggle = new PresetRangeToggle();
        private readonly PresetMultiSelection<DirectoryEntry> _selectedDirectoryRows = new PresetMultiSelection<DirectoryEntry>();

        // 输入
        private string _newDirPath = string.Empty;
        private string _newPkgName = string.Empty, _newPkgDisplay = string.Empty, _newPkgSpec = string.Empty;
        private SettingsEntry.Category _newSetCategory = SettingsEntry.Category.PlayerName;
        private string _newSetKey = "companyName", _newSetValue = string.Empty;

        // 状态
        private PackageInstaller _packageInstaller;
        private string _statusMessage = string.Empty;
        private bool _dirDragHover, _pkgDragHover, _pluginDragHover;

        // 颜色
        private static readonly Color ClrBg = new Color(0.16f, 0.16f, 0.17f);
        private static readonly Color ClrItemAlt = new Color(0.21f, 0.21f, 0.22f, 0.5f);
        private static readonly Color ClrAccent = new Color(0.30f, 0.55f, 0.95f);
        private static readonly Color ClrDim = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color ClrGreen = new Color(0.35f, 0.75f, 0.45f);
        private static readonly Color ClrYellow = new Color(0.85f, 0.75f, 0.25f);
        private static readonly Color ClrRed = new Color(0.80f, 0.45f, 0.35f);
        private static readonly Color ClrOrange = new Color(0.90f, 0.65f, 0.25f);

        // 样式缓存
        private GUIStyle _stMiniBold, _stMini, _stRowLabel;
        private bool _stylesReady;

        private void InitStyles()
        {
            if (_stylesReady) return;
            _stMiniBold = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            _stMini = new GUIStyle(EditorStyles.label) { fontSize = 10, normal = { textColor = ClrDim } };
            _stRowLabel = new GUIStyle(EditorStyles.label) { fontSize = 11 };
            _stylesReady = true;
        }

        /// <summary>绘制功能区域标题 — 左侧色条 + 粗体标题。</summary>
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

        [MenuItem("Tools/项目初始化预设编辑器", priority = 1)]
        public static void ShowWindow()
        {
            var w = GetWindow<PresetEditorWindow>("预设编辑器");
            w.minSize = new Vector2(620, 580);
        }

        public static void ShowWindow(ProjectInitPreset preset, bool isNew = false)
        {
            var w = GetWindow<PresetEditorWindow>("预设编辑器");
            w.minSize = new Vector2(620, 580);
            w.LoadPreset(preset, isNew);
        }

        public void LoadPreset(ProjectInitPreset preset, bool isNew = false)
        {
            string path = preset == null ? string.Empty : AssetDatabase.GetAssetPath(preset);
            bool local = path.StartsWith(PresetManager.PresetFolder + "/", StringComparison.Ordinal);
            _preset = preset != null && !local && !string.IsNullOrEmpty(path) ? preset.Clone() : preset;
            _isNewPreset = isNew || (!local && !string.IsNullOrEmpty(path));
            _dirRangeToggle.Reset();
            _pkgRangeToggle.Reset();
            _pluginRangeToggle.Reset();
            _setRangeToggle.Reset();
            _selectedDirectoryRows.Clear();
            _statusMessage = string.Empty;
        }

        private void OnEnable() => _packageInstaller = new PackageInstaller();

        private void OnGUI()
        {
            InitStyles();
            if (_preset == null) { DrawNoPresetState(); return; }

            EditorGUILayout.Space(4);
            DrawSection("预设信息", new Color(0.30f, 0.55f, 0.95f));
            EditorGUILayout.Space(2);
            DrawHeader();
            EditorGUILayout.Space(6);
            DrawTabBar();
            EditorGUILayout.Space(4);

            switch (_currentTab)
            {
                case Tab.Directories: DrawDirectoriesTab(); break;
                case Tab.Packages: DrawPackagesTab(); break;
                case Tab.Plugins: DrawPluginsTab(); break;
                case Tab.Settings: DrawSettingsTab(); break;
            }

            DrawFooter();
        }

        #region Header & Tabs

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.BeginChangeCheck();
            _preset.presetName = EditorGUILayout.TextField("预设名称", _preset.presetName);
            EditorGUILayout.LabelField("描述");
            _descScroll = EditorGUILayout.BeginScrollView(_descScroll, GUILayout.Height(40));
            _preset.description = EditorGUILayout.TextArea(_preset.description, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            if (EditorGUI.EndChangeCheck()) MarkDirty();
            EditorGUILayout.LabelField(
                $"目录 {_preset.directories?.Count ?? 0}    依赖包 {_preset.packages?.Count ?? 0}    插件文件 {_preset.plugins?.Count ?? 0}    设置 {_preset.settings?.Count ?? 0}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_currentTab == Tab.Directories, "📁 目录模板", "LargeButton", GUILayout.Height(26)))
                _currentTab = Tab.Directories;
            if (GUILayout.Toggle(_currentTab == Tab.Packages, "📦 依赖包", "LargeButton", GUILayout.Height(26)))
                _currentTab = Tab.Packages;
            if (GUILayout.Toggle(_currentTab == Tab.Plugins, "🧩 插件文件", "LargeButton", GUILayout.Height(26)))
                _currentTab = Tab.Plugins;
            if (GUILayout.Toggle(_currentTab == Tab.Settings, "⚙ 项目设置", "LargeButton", GUILayout.Height(26)))
                _currentTab = Tab.Settings;
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Plugins Tab

        private void DrawPluginsTab()
        {
            if (_preset.plugins == null) _preset.plugins = new List<PluginEntry>();
            DrawSection($"插件文件 ({_preset.plugins.Count})", ClrPurple());
            EditorGUILayout.HelpBox("保存预设时会把列出的插件完整打包（含 .meta）。应用时仅复制勾选项；目标已存在的插件会跳过。复制预设到新项目时请连同 .plugins.bytes 文件一起复制。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("读取 Assets/Plugins", GUILayout.Width(145))) ReadCurrentProjectPlugins();
                if (GUILayout.Button("全选", GUILayout.Width(50))) { foreach (var p in _preset.plugins) p.copyFiles = true; MarkDirty(); }
                if (GUILayout.Button("全不选", GUILayout.Width(60))) { foreach (var p in _preset.plugins) p.copyFiles = false; MarkDirty(); }
                GUILayout.FlexibleSpace();
                GUILayout.Label($"复制 {_preset.SelectedPluginCount}/{_preset.plugins.Count}", _stMiniBold);
            }
            DrawDropArea("拖入 Assets/Plugins 中的目录或文件", ref _pluginDragHover, HandlePluginDragDrop);
            if (_preset.pluginArchive != null)
                EditorGUILayout.LabelField("文件归档：" + AssetDatabase.GetAssetPath(_preset.pluginArchive), _stMini);
            else
                EditorGUILayout.LabelField("文件归档：保存预设后生成", _stMini);

            _pluginScroll = EditorGUILayout.BeginScrollView(_pluginScroll, GUILayout.ExpandHeight(true));
            if (_preset.plugins.Count == 0)
                EditorGUILayout.HelpBox("尚未添加插件。点击读取或将插件拖入上方区域。", MessageType.None);
            for (int i = 0; i < _preset.plugins.Count; i++)
            {
                var plugin = _preset.plugins[i];
                if (plugin == null) continue;
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    if (_pluginRangeToggle.Draw(_preset.plugins, i, plugin.copyFiles, 18f,
                            (entry, value) => { if (entry != null) entry.copyFiles = value; }))
                    {
                        MarkDirty();
                    }
                    GUILayout.Label(plugin.isDirectory ? "📁" : "📄", GUILayout.Width(22));
                    using (new EditorGUILayout.VerticalScope())
                    {
                        GUILayout.Label(Path.GetFileName(plugin.path), _stMiniBold);
                        GUILayout.Label(plugin.path, _stMini);
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(PluginArchiveManager.PluginExists(plugin) ? "已在项目中" : "等待复制", _stMini, GUILayout.Width(70));
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                    { _preset.plugins.RemoveAt(i--); _pluginRangeToggle.Reset(); MarkDirty(); }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static Color ClrPurple() => new Color(0.55f, 0.45f, 0.85f);

        private void ReadCurrentProjectPlugins()
        {
            _preset.sourceProjectRoot = PresetManager.CurrentProjectRoot;
            int added = 0;
            foreach (var plugin in PresetManager.ReadCurrentProjectPlugins())
            {
                if (_preset.plugins.Any(p => p != null && p.path == plugin.path)) continue;
                _preset.plugins.Add(plugin);
                added++;
            }
            if (added > 0) MarkDirty();
            _statusMessage = $"添加了 {added} 个插件条目。";
        }

        private void HandlePluginDragDrop()
        {
            int added = 0;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (path.StartsWith("Assets/", StringComparison.Ordinal)) path = path.Substring(7);
                if (!PluginArchiveManager.IsValidPluginPath(path) || _preset.plugins.Any(p => p != null && p.path == path)) continue;
                string fullPath = Path.Combine(Application.dataPath, path);
                if (!Directory.Exists(fullPath) && !File.Exists(fullPath)) continue;
                _preset.plugins.Add(new PluginEntry(path, Directory.Exists(fullPath)));
                added++;
            }
            if (added > 0) MarkDirty();
            _statusMessage = $"拖入了 {added} 个插件条目。";
        }

        #endregion

        #region Directories Tab

        private void DrawDirectoriesTab()
        {
            DrawSection($"目录模板 ({_preset.directories.Count})", new Color(0.35f, 0.70f, 0.75f));
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginVertical("box");

            // 工具栏
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("标准游戏项目", GUILayout.Width(100)))
                    AddStandardDirectories();
                if (GUILayout.Button("UI 项目", GUILayout.Width(70)))
                    AddUIDirectories();
                if (GUILayout.Button("读取当前项目", GUILayout.Width(90)))
                    ReadCurrentProjectDirectories();
                if (GUILayout.Button("全选", GUILayout.Width(50)))
                { foreach (var entry in _preset.directories) if (entry != null) entry.enabled = true; _dirRangeToggle.Reset(); MarkDirty(); }
                if (GUILayout.Button("全不选", GUILayout.Width(60)))
                { foreach (var entry in _preset.directories) if (entry != null) entry.enabled = false; _dirRangeToggle.Reset(); MarkDirty(); }
                if (GUILayout.Button("清空", GUILayout.Width(45)))
                { _preset.directories.Clear(); _dirRangeToggle.Reset(); _selectedDirectoryRows.Clear(); MarkDirty(); }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"已选目录 {_selectedDirectoryRows.Count}", _stMini, GUILayout.Width(80));
                EditorGUI.BeginDisabledGroup(_selectedDirectoryRows.Count == 0);
                if (GUILayout.Button("勾选所选", GUILayout.Width(75)))
                { _selectedDirectoryRows.Apply(true, (entry, value) => entry.enabled = value); MarkDirty(); }
                if (GUILayout.Button("取消所选", GUILayout.Width(75)))
                { _selectedDirectoryRows.Apply(false, (entry, value) => entry.enabled = value); MarkDirty(); }
                if (GUILayout.Button("清除选择", GUILayout.Width(75)))
                    _selectedDirectoryRows.Clear();
                EditorGUI.EndDisabledGroup();
                GUILayout.FlexibleSpace();
            }

            // 拖拽区
            DrawDropArea("拖拽文件夹到此处添加目录", ref _dirDragHover, HandleDirectoryDragDrop);

            // 添加输入
            using (new EditorGUILayout.HorizontalScope())
            {
                _newDirPath = EditorGUILayout.TextField(_newDirPath);
                if (GUILayout.Button("+ 添加", GUILayout.Width(60)) && !string.IsNullOrWhiteSpace(_newDirPath))
                {
                    _preset.directories.Add(new DirectoryEntry(_newDirPath));
                    _newDirPath = string.Empty;
                    MarkDirty();
                }
            }

            // 列表 — 分层树显示
            EditorGUILayout.LabelField("Shift 点击首尾复选框可连续勾选；也可点击名称选行后批量处理。", _stMini);
            _dirScroll = EditorGUILayout.BeginScrollView(_dirScroll, GUILayout.ExpandHeight(true));

            if (_preset.directories.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无目录。拖拽文件夹、输入路径、或点击快速模板。", MessageType.Info);
            }
            else
            {
                DrawDirectoryTreeList();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ── 分层树显示 ──

        private class DirNode
        {
            public string name;
            public DirectoryEntry entry;
            public List<DirNode> children = new List<DirNode>();
        }

        private static DirNode BuildDirTree(List<DirectoryEntry> entries)
        {
            var root = new DirNode { name = "Assets" };
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.path)) continue;
                var parts = entry.path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                var current = root;
                for (int i = 0; i < parts.Length; i++)
                {
                    var child = current.children.FirstOrDefault(c => c.name == parts[i]);
                    if (child == null) { child = new DirNode { name = parts[i] }; current.children.Add(child); }
                    if (i == parts.Length - 1) child.entry = entry;
                    current = child;
                }
            }
            return root;
        }

        private void DrawDirectoryTreeList()
        {
            var root = BuildDirTree(_preset.directories);
            var visibleEntries = new List<DirectoryEntry>();
            CollectDirectoryEntries(root, visibleEntries);
            _selectedDirectoryRows.Prune(visibleEntries);
            foreach (var child in root.children)
                DrawDirNode(child, 1, visibleEntries, true);
        }

        private static void CollectDirectoryEntries(DirNode node, List<DirectoryEntry> entries)
        {
            if (node.entry != null) entries.Add(node.entry);
            foreach (var child in node.children)
                CollectDirectoryEntries(child, entries);
        }

        private void DrawDirNode(DirNode node, int depth, List<DirectoryEntry> visibleEntries,
            bool parentEnabled)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(depth * 16);

                if (node.entry != null)
                {
                    bool exists = DirectoryTemplateCreator.DirectoryExists(node.entry.path);
                    if (_dirRangeToggle.Draw(visibleEntries, visibleEntries.IndexOf(node.entry),
                            node.entry.enabled, 18f, (entry, value) => entry.enabled = value))
                    {
                        MarkDirty();
                    }
                    bool rowShift = Event.current.shift;
                    bool additive = Event.current.control || Event.current.command;
                    if (PresetDirectoryRow.Draw(node.name, _selectedDirectoryRows.Contains(node.entry),
                            !parentEnabled))
                        _selectedDirectoryRows.Click(visibleEntries, visibleEntries.IndexOf(node.entry), rowShift, additive);

                    var c = GUI.color;
                    GUI.color = exists ? ClrGreen : ClrDim;
                    GUILayout.Label(new GUIContent(exists ? "✓" : "○", exists ? "目录已存在" : "待创建"),
                        _stMini, GUILayout.Width(18));
                    GUI.color = c;

                    if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        _preset.directories.Remove(node.entry);
                        _dirRangeToggle.Reset();
                        _selectedDirectoryRows.Clear();
                        MarkDirty();
                    }
                }
                else
                {
                    Color originalContentColor = GUI.contentColor;
                    if (!parentEnabled)
                        GUI.contentColor = new Color(originalContentColor.r, originalContentColor.g,
                            originalContentColor.b, originalContentColor.a * 0.45f);
                    GUILayout.Label($"📂 {node.name}", _stMiniBold);
                    GUI.contentColor = originalContentColor;
                    GUILayout.FlexibleSpace();
                }

                if (node.children.Count > 0)
                {
                    var groupEntries = new List<DirectoryEntry>();
                    CollectDirectoryEntries(node, groupEntries);
                    GUILayout.Label($"{groupEntries.Count(e => e.enabled)}/{groupEntries.Count}",
                        _stMini, GUILayout.Width(38));
                    PresetDirectoryGroupMenu.Draw(groupEntries, () =>
                    { _dirRangeToggle.Reset(); MarkDirty(); Repaint(); });
                }
            }

            bool childrenEnabled = parentEnabled && (node.entry == null || node.entry.enabled);
            foreach (var child in node.children)
                DrawDirNode(child, depth + 1, visibleEntries, childrenEnabled);
        }

        private void HandleDirectoryDragDrop()
        {
            int added = 0;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is DefaultAsset folder)
                {
                    string assetPath = AssetDatabase.GetAssetPath(folder);
                    if (!string.IsNullOrEmpty(assetPath) && assetPath.StartsWith("Assets/"))
                    {
                        string relPath = assetPath.Substring(7);
                        if (!_preset.directories.Any(d => d.path == relPath))
                        { _preset.directories.Add(new DirectoryEntry(relPath)); added++; }
                    }
                }
            }
            if (added > 0) { _statusMessage = $"拖拽添加了 {added} 个目录。"; MarkDirty(); }
        }

        private void ReadCurrentProjectDirectories()
        {
            string assetsPath = Application.dataPath;
            int added = 0;
            foreach (var dir in Directory.GetDirectories(assetsPath, "*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(assetsPath, dir).Replace('\\', '/');
                if (IsUnityInternalFolder(relPath)) continue;
                if (!_preset.directories.Any(d => d.path == relPath))
                { _preset.directories.Add(new DirectoryEntry(relPath)); added++; }
            }
            _statusMessage = added > 0 ? $"读取项目添加了 {added} 个目录。" : "项目目录已全部在预设中。";
            MarkDirty();
        }

        private static bool IsUnityInternalFolder(string relPath)
        {
            var skip = new[] { "Plugins", "StreamingAssets", "Resources" };
            string top = relPath.Contains('/') ? relPath.Substring(0, relPath.IndexOf('/')) : relPath;
            return skip.Contains(top, StringComparer.OrdinalIgnoreCase);
        }

        private void AddStandardDirectories()
        {
            var dirs = new[] { "Animations", "Audio/Music", "Audio/SFX", "Materials", "Models",
                "Prefabs", "Scenes", "Scripts/Runtime", "Scripts/Editor", "Settings",
                "Shaders", "Sprites", "Textures", "UI" };
            foreach (var d in dirs)
                if (!_preset.directories.Any(x => x.path == d))
                    _preset.directories.Add(new DirectoryEntry(d));
            MarkDirty();
        }

        private void AddUIDirectories()
        {
            var dirs = new[] { "UI", "UI/Sprites", "UI/Fonts", "UI/Prefabs", "UI/Materials",
                "Scripts/Runtime/UI", "Art", "Art/Icons", "Art/Backgrounds" };
            foreach (var d in dirs)
                if (!_preset.directories.Any(x => x.path == d))
                    _preset.directories.Add(new DirectoryEntry(d));
            MarkDirty();
        }

        #endregion

        #region Packages Tab

        private void DrawPackagesTab()
        {
            DrawSection($"依赖包 ({_preset.packages.Count})", new Color(0.55f, 0.45f, 0.85f));
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginVertical("box");

            // 常用包
            DrawCommonPackageList();

            EditorGUILayout.Space(2);

            // 读取项目 + 拖拽
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("读取当前项目包", GUILayout.Width(110)))
                    ReadCurrentProjectPackages();
                EditorGUILayout.LabelField("扫描已安装的 UPM 包", _stMini);
            }

            DrawDropArea("拖拽 package.json 或文件夹到此处", ref _pkgDragHover, HandlePackageDragDrop);

            // 添加新包
            using (new EditorGUILayout.HorizontalScope())
            {
                _newPkgDisplay = EditorGUILayout.TextField(_newPkgDisplay, GUILayout.Width(100));
                _newPkgName = EditorGUILayout.TextField(_newPkgName, GUILayout.Width(140));
                _newPkgSpec = EditorGUILayout.TextField(_newPkgSpec);
                if (GUILayout.Button("+", GUILayout.Width(24)) && !string.IsNullOrWhiteSpace(_newPkgSpec))
                {
                    if (string.IsNullOrEmpty(_newPkgName)) _newPkgName = _newPkgSpec;
                    if (string.IsNullOrEmpty(_newPkgDisplay)) _newPkgDisplay = _newPkgName;
                    _preset.packages.Add(new PackageEntry(_newPkgName, _newPkgDisplay, _newPkgSpec));
                    _newPkgName = _newPkgDisplay = _newPkgSpec = string.Empty;
                    MarkDirty();
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("显示名", _stMini, GUILayout.Width(100));
                EditorGUILayout.LabelField("包名", _stMini, GUILayout.Width(140));
                EditorGUILayout.LabelField("安装规格 (包名 / Git URL)", _stMini);
            }

            // 工具栏
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("检查安装状态", GUILayout.Width(100)))
                    CheckPackageStatus();
                if (_preset.packages.Count > 0)
                    EditorGUILayout.LabelField($"已选中: {_preset.SelectedPackageCount}/{_preset.packages.Count}", _stMini);
            }

            // 包列表
            _pkgScroll = EditorGUILayout.BeginScrollView(_pkgScroll, GUILayout.ExpandHeight(true));

            if (_preset.packages.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无依赖包。点击上方常用包、拖拽 package.json、或手动添加。", MessageType.Info);
            }
            else
            {
                var installed = _packageInstaller.GetInstalledPackageNames();
                for (int i = 0; i < _preset.packages.Count; i++)
                {
                    var entry = _preset.packages[i];
                    if (entry == null) continue;
                    bool isInstalled = installed != null && installed.Contains(entry.packageName);
                    bool isGit = !string.IsNullOrEmpty(entry.installSpec) && entry.installSpec.StartsWith("http");
                    Color bar = isInstalled ? ClrGreen : isGit ? ClrYellow : ClrAccent;

                    // ── 行1: 勾选 + 显示名 + 状态 + 操作 ──
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (i % 2 == 0) DrawRowBg(ClrItemAlt);
                        GUILayout.Space(4);
                        DrawColorBar(bar, 3, 36);

                        if (_pkgRangeToggle.Draw(_preset.packages, i, entry.selected, 18f,
                                (item, value) => { if (item != null) item.selected = value; }))
                        {
                            MarkDirty();
                        }
                        EditorGUI.BeginChangeCheck();
                        entry.displayName = EditorGUILayout.TextField(entry.displayName, GUILayout.Height(18));
                        if (EditorGUI.EndChangeCheck()) MarkDirty();

                        var c = GUI.color;
                        GUI.color = isInstalled ? ClrGreen : ClrOrange;
                        GUILayout.Label(isInstalled ? "● 已安装" : "○ 未安装", _stMini, GUILayout.Width(55));
                        GUI.color = c;

                        if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                        { _preset.packages.RemoveAt(i); _pkgRangeToggle.Reset(); MarkDirty(); }
                    }

                    // ── 行2: 包名 + 规格 ──
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (i % 2 == 0) DrawRowBg(ClrItemAlt);
                        GUILayout.Space(7 + 3 + 18 + 4); // 对齐色条+勾选

                        EditorGUI.BeginChangeCheck();
                        entry.packageName = EditorGUILayout.TextField(entry.packageName, GUILayout.Height(16));
                        GUILayout.Space(2);
                        entry.installSpec = EditorGUILayout.TextField(entry.installSpec, GUILayout.Height(16));
                        if (EditorGUI.EndChangeCheck()) MarkDirty();

                        GUILayout.Space(22 + 55 + 4); // 对齐右侧按钮+状态
                    }
                    GUILayout.Space(1);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawCommonPackageList()
        {
            EditorGUILayout.LabelField("常用包", _stMiniBold);

            // ── Unity 官方 ──
            EditorGUILayout.LabelField("Unity 官方", _stMini, GUILayout.Width(60));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16);
                if (GUILayout.Button("+ Addressables", GUILayout.ExpandWidth(false)))
                    TryAddPackage("com.unity.addressables", "Addressables", "com.unity.addressables");
                if (GUILayout.Button("+ Localization", GUILayout.ExpandWidth(false)))
                    TryAddPackage("com.unity.localization", "Localization", "com.unity.localization");
                if (GUILayout.Button("+ Post Processing", GUILayout.ExpandWidth(false)))
                    TryAddPackage("com.unity.postprocessing", "Post Processing", "com.unity.postprocessing");
                if (GUILayout.Button("+ Newtonsoft Json", GUILayout.ExpandWidth(false)))
                    TryAddPackage("com.unity.nuget.newtonsoft-json", "Newtonsoft Json", "com.unity.nuget.newtonsoft-json");
                GUILayout.FlexibleSpace();
            }

            // ── 第三方 ──
            EditorGUILayout.LabelField("第三方", _stMini, GUILayout.Width(60));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16);
                if (GUILayout.Button("+ UniTask", GUILayout.ExpandWidth(false)))
                    TryAddPackage("com.cysharp.unitask", "UniTask",
                        "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask");
                if (GUILayout.Button("+ UnityTimer", GUILayout.ExpandWidth(false)))
                    TryAddPackage("com.akbiggs.unitytimer", "UnityTimer",
                        "https://github.com/akbiggs/UnityTimer.git");
                GUILayout.FlexibleSpace();
            }

            // ── UnityFramework ──
            EditorGUILayout.LabelField("UnityFramework", _stMini, GUILayout.Width(60));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16);
                if (GUILayout.Button("+ Core", GUILayout.ExpandWidth(false)))
                    TryAddPackage("com.unityframework.core", "UnityFramework",
                        PresetManager.UnityFrameworkInstallSpec);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(32);
                EditorGUILayout.LabelField("├", _stMini, GUILayout.Width(12));
                if (GUILayout.Button("+ ZEventSystem", GUILayout.ExpandWidth(false)))
                    TryAddPackage("com.zko.zeventsystem", "ZEventSystem",
                        "https://gitee.com/PN-BUG/infinite-treasury.git?path=Assets/UnityFramework/Runtime/ZEventSystem");
                EditorGUILayout.LabelField("├", _stMini, GUILayout.Width(12));
                if (GUILayout.Button("+ UnityToolsHub", GUILayout.ExpandWidth(false)))
                    TryAddPackage("com.zko.unitytoolshub", "UnityToolsHub",
                        "https://gitee.com/PN-BUG/infinite-treasury.git?path=Assets/UnityFramework/Editor/UnityToolsHub");
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(32);
                EditorGUILayout.LabelField("├", _stMini, GUILayout.Width(12));
                if (GUILayout.Button("+ Nodin", GUILayout.ExpandWidth(false)))
                    TryAddPackage("com.zko.nodin", "Nodin",
                        "https://gitee.com/PN-BUG/infinite-treasury.git?path=Assets/UnityFramework/Editor/UnityToolsHub/Editor/Nodin");
                EditorGUILayout.LabelField("└", _stMini, GUILayout.Width(12));
                if (GUILayout.Button("+ PackageCreator", GUILayout.ExpandWidth(false)))
                    TryAddPackage("com.zko.unitypackagecreator", "PackageCreator",
                        "https://gitee.com/PN-BUG/infinite-treasury.git?path=Assets/UnityFramework/Editor/UnityToolsHub/Editor/Tools/Unity Package Creator");
                GUILayout.FlexibleSpace();
            }
        }

        private void TryAddPackage(string pkgName, string displayName, string installSpec)
        {
            if (!_preset.packages.Any(p => p.packageName == pkgName))
            {
                _preset.packages.Add(new PackageEntry(pkgName, displayName, installSpec));
                MarkDirty();
            }
        }

        private void HandlePackageDragDrop()
        {
            int added = 0;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(assetPath)) continue;

                string pkgJsonPath = null;
                if (assetPath.EndsWith("package.json", StringComparison.OrdinalIgnoreCase))
                    pkgJsonPath = assetPath;
                else if (obj is DefaultAsset)
                {
                    string candidate = Path.Combine(assetPath, "package.json");
                    if (File.Exists(Path.GetFullPath(candidate)))
                        pkgJsonPath = candidate;
                }
                if (pkgJsonPath == null) continue;

                var parsed = ParsePackageJson(pkgJsonPath);
                if (parsed == null || parsed.Length < 2) continue;
                if (!_preset.packages.Any(p => p.packageName == parsed[0]))
                { _preset.packages.Add(new PackageEntry(parsed[0], parsed[1], parsed[0])); added++; }
            }
            if (added > 0) { _statusMessage = $"拖拽添加了 {added} 个包。"; MarkDirty(); }
        }

        private void ReadCurrentProjectPackages()
        {
            int added = 0;
            foreach (var package in PresetManager.ReadProjectPackages())
            {
                if (_preset.packages.Any(p => p.packageName == package.packageName)) continue;
                _preset.packages.Add(package);
                added++;
            }
            _statusMessage = added > 0 ? $"读取项目添加了 {added} 个包。" : "项目依赖已全部在预设中。";
            if (added > 0) MarkDirty();
        }

        private static string[] ParsePackageJson(string assetPath)
        {
            try
            {
                string fullPath = Path.GetFullPath(assetPath);
                if (!File.Exists(fullPath)) return null;
                string json = File.ReadAllText(fullPath);
                string name = ExtractJsonValue(json, "name");
                string displayName = ExtractJsonValue(json, "displayName");
                if (string.IsNullOrEmpty(displayName)) displayName = name;
                if (string.IsNullOrEmpty(name)) return null;
                return new[] { name, displayName };
            }
            catch { return null; }
        }

        private static string ExtractJsonValue(string json, string key)
        {
            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            idx = json.IndexOf(':', idx + pattern.Length);
            if (idx < 0) return null;
            idx = json.IndexOf('"', idx + 1);
            if (idx < 0) return null;
            int end = json.IndexOf('"', idx + 1);
            if (end < 0) return null;
            return json.Substring(idx + 1, end - idx - 1);
        }

        private void CheckPackageStatus()
        {
            _statusMessage = "正在检查包安装状态...";
            _packageInstaller.CheckPackages(_preset.packages, result =>
            {
                _statusMessage = result.success
                    ? $"检查完成: 已安装 {result.installedPackages.Count}, 未安装 {result.missingPackages.Count}"
                    : $"检查失败: {result.errorMessage}";
                Repaint();
            });
        }

        #endregion

        #region Settings Tab

        private void DrawSettingsTab()
        {
            DrawSection($"项目设置 ({_preset.settings.Count})", new Color(0.80f, 0.65f, 0.25f));
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginVertical("box");

            // 工具栏
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("默认游戏设置", GUILayout.Width(100)))
                    AddDefaultSettings();
                if (GUILayout.Button("读取当前项目", GUILayout.Width(90)))
                    ReadCurrentProjectSettings();
                if (GUILayout.Button("清空", GUILayout.Width(45)))
                { _preset.settings.Clear(); MarkDirty(); }
            }

            // 添加新设置
            using (new EditorGUILayout.HorizontalScope())
            {
                _newSetCategory = (SettingsEntry.Category)EditorGUILayout.EnumPopup(_newSetCategory, GUILayout.Width(130));
                _newSetKey = EditorGUILayout.TextField(_newSetKey, GUILayout.Width(120));
                EditorGUILayout.LabelField("=", GUILayout.Width(10));
                _newSetValue = EditorGUILayout.TextField(_newSetValue);
                if (GUILayout.Button("+", GUILayout.Width(24)) && !string.IsNullOrWhiteSpace(_newSetKey))
                {
                    _preset.settings.Add(new SettingsEntry(_newSetCategory, _newSetKey, _newSetValue));
                    _newSetKey = _newSetValue = string.Empty;
                    MarkDirty();
                }
            }

            // 列表
            _setScroll = EditorGUILayout.BeginScrollView(_setScroll, GUILayout.ExpandHeight(true));

            if (_preset.settings.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无设置项。使用上方表单添加，或点击快速模板。", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < _preset.settings.Count; i++)
                {
                    var entry = _preset.settings[i];
                    if (entry == null) continue;
                    Color bar = GetCategoryColor(entry.category);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (i % 2 == 0) DrawRowBg(ClrItemAlt);
                        GUILayout.Space(4);
                        DrawColorBar(bar, 3, 20);

                        if (_setRangeToggle.Draw(_preset.settings, i, entry.enabled, 18f,
                                (item, value) => { if (item != null) item.enabled = value; }))
                        {
                            MarkDirty();
                        }
                        EditorGUI.BeginChangeCheck();
                        entry.category = (SettingsEntry.Category)EditorGUILayout.EnumPopup(entry.category, GUILayout.Width(110), GUILayout.Height(18));
                        entry.key = EditorGUILayout.TextField(entry.key, GUILayout.Width(120), GUILayout.Height(18));
                        EditorGUILayout.LabelField("=", GUILayout.Width(10));
                        entry.value = EditorGUILayout.TextField(entry.value, GUILayout.Height(18));
                        if (EditorGUI.EndChangeCheck()) MarkDirty();

                        if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                        { _preset.settings.RemoveAt(i); _setRangeToggle.Reset(); MarkDirty(); }
                    }
                    GUILayout.Space(1);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static Color GetCategoryColor(SettingsEntry.Category cat) => cat switch
        {
            SettingsEntry.Category.PlayerName => new Color(0.4f, 0.6f, 0.8f),
            SettingsEntry.Category.ScriptingDefines => new Color(0.55f, 0.45f, 0.85f),
            SettingsEntry.Category.Android => ClrGreen,
            SettingsEntry.Category.iOS => ClrOrange,
            SettingsEntry.Category.Graphics => new Color(0.8f, 0.5f, 0.6f),
            _ => ClrDim,
        };

        private void ReadCurrentProjectSettings()
        {
            var current = new List<SettingsEntry>
            {
                new(SettingsEntry.Category.PlayerName, "companyName", PlayerSettings.companyName),
                new(SettingsEntry.Category.PlayerName, "productName", PlayerSettings.productName),
                new(SettingsEntry.Category.PlayerName, "bundleVersion", PlayerSettings.bundleVersion),
                new(SettingsEntry.Category.Graphics, "colorSpace", PlayerSettings.colorSpace == ColorSpace.Linear ? "Linear" : "Gamma"),
            };
            int added = 0;
            foreach (var s in current)
                if (!_preset.settings.Any(x => x.category == s.category && x.key == s.key))
                { _preset.settings.Add(new SettingsEntry(s.category, s.key, s.value)); added++; }
            _statusMessage = added > 0 ? $"读取项目添加了 {added} 项设置。" : "项目设置已全部在预设中。";
            MarkDirty();
        }

        private void AddDefaultSettings()
        {
            var defaults = new[]
            {
                new SettingsEntry(SettingsEntry.Category.PlayerName, "companyName", "DefaultCompany"),
                new SettingsEntry(SettingsEntry.Category.PlayerName, "productName", "GameProject"),
                new SettingsEntry(SettingsEntry.Category.PlayerName, "bundleVersion", "0.1.0"),
                new SettingsEntry(SettingsEntry.Category.Graphics, "colorSpace", "Linear"),
                new SettingsEntry(SettingsEntry.Category.Android, "scriptingBackend", "il2cpp"),
            };
            foreach (var s in defaults)
                if (!_preset.settings.Any(x => x.category == s.category && x.key == s.key))
                    _preset.settings.Add(new SettingsEntry(s.category, s.key, s.value));
            MarkDirty();
        }

        #endregion

        #region Drop Area

        private void DrawDropArea(string label, ref bool dragHover, Action onDrop)
        {
            Rect rect = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
            bool isDragging = rect.Contains(Event.current.mousePosition);

            switch (Event.current.type)
            {
                case EventType.DragUpdated:
                    if (isDragging) { DragAndDrop.visualMode = DragAndDropVisualMode.Copy; dragHover = true; }
                    break;
                case EventType.DragPerform:
                    if (isDragging) { DragAndDrop.AcceptDrag(); dragHover = false; onDrop?.Invoke(); Event.current.Use(); }
                    break;
                case EventType.DragExited:
                    dragHover = false;
                    break;
            }

            EditorGUI.DrawRect(rect, dragHover ? new Color(0.3f, 0.5f, 0.8f, 0.25f) : new Color(0, 0, 0, 0.15f));
            var border = dragHover ? new Color(0.4f, 0.6f, 0.9f) : new Color(1, 1, 1, 0.1f);
            DrawRectBorder(rect, border);
            var style = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11,
                normal = { textColor = dragHover ? new Color(0.6f, 0.8f, 1f) : ClrDim } };
            GUI.Label(rect, dragHover ? "松开以添加" : label, style);
        }

        private static void DrawRectBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }

        #endregion

        #region Row helpers

        /// <summary>no-op — 在 auto layout 中无法可靠获取行 rect 画背景，留空避免报错。</summary>
        private static void DrawRowBg(Color color) { }

        private static void DrawColorBar(Color color, float width, float height)
        {
            Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width));
            EditorGUI.DrawRect(rect, color);
        }

        #endregion

        #region Footer

        private void DrawFooter()
        {
            if (!string.IsNullOrEmpty(_statusMessage))
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("保存预设", GUILayout.Height(26)))
                    SavePreset();
                if (GUILayout.Button("另存为...", GUILayout.Height(26)))
                    SavePresetAs();
                if (GUILayout.Button("从默认预设导入", GUILayout.Height(26)))
                    ImportFromPreset(PresetManager.CreateDefaultPreset());
            }
        }

        private void SavePreset()
        {
            string path = AssetDatabase.GetAssetPath(_preset);
            if (path.StartsWith(PresetManager.PresetFolder + "/", StringComparison.Ordinal))
            { MarkDirty(); bool archived = PluginArchiveManager.RefreshArchive(_preset); AssetDatabase.SaveAssets(); _statusMessage = archived ? $"已保存: {path}" : "预设已保存，但插件归档未生成；请检查插件源文件。"; }
            else SavePresetAs();
        }

        private void SavePresetAs()
        {
            PresetManager.EnsurePresetFolder();
            string path = EditorUtility.SaveFilePanelInProject("保存预设", _preset.presetName, "asset", "选择位置", PresetManager.PresetFolder);
            if (string.IsNullOrEmpty(path)) return;
            if (!path.StartsWith(PresetManager.PresetFolder + "/", StringComparison.Ordinal))
            { _statusMessage = "请将预设保存到本地 Presets 目录，避免上传 Git。"; return; }
            var copy = _preset.Clone();
            path = PresetManager.SavePreset(copy, Path.GetFileNameWithoutExtension(path));
            _preset = copy;
            _isNewPreset = false;
            _statusMessage = copy.plugins.Count == 0 || copy.pluginArchive != null
                ? $"已保存: {path}" : "预设已保存，但插件归档未生成；请检查插件源文件。";
        }

        private void ImportFromPreset(ProjectInitPreset src)
        {
            var copy = src.Clone();
            _preset.directories = copy.directories;
            _preset.packages = copy.packages;
            _preset.plugins = copy.plugins;
            _preset.pluginArchive = copy.pluginArchive;
            _preset.settings = copy.settings;
            DestroyImmediate(copy);
            MarkDirty();
            _statusMessage = "已从预设导入内容。";
        }

        #endregion

        #region No Preset

        private void DrawNoPresetState()
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("未加载预设", EditorStyles.boldLabel);
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox("请选择一个操作：", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("新建空白预设", GUILayout.Height(28)))
                    LoadPreset(PresetManager.CreateEmptyPreset(), true);
                if (GUILayout.Button("从默认预设创建", GUILayout.Height(28)))
                    LoadPreset(PresetManager.CreateDefaultPreset(), true);
                if (GUILayout.Button("从当前项目创建", GUILayout.Height(28)))
                    LoadPreset(PresetManager.CreateFromCurrentProject(), true);
                if (GUILayout.Button("从其他项目创建", GUILayout.Height(28)))
                {
                    string project = EditorUtility.OpenFolderPanel("选择 Unity 项目根目录", string.Empty, string.Empty);
                    if (!string.IsNullOrEmpty(project))
                    {
                        if (PresetManager.IsUnityProject(project))
                            LoadPreset(PresetManager.CreateFromProject(project), true);
                        else _statusMessage = "请选择包含 Assets、Packages 和 ProjectSettings 的 Unity 项目根目录。";
                    }
                }
            }
            EditorGUILayout.Space(4);
            if (!string.IsNullOrEmpty(_statusMessage))
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Warning);
            EditorGUILayout.LabelField("已有预设:", EditorStyles.miniLabel);
            var all = PresetManager.FindAllPresets();
            if (all.Count == 0) { EditorGUILayout.HelpBox("暂无已保存的预设。", MessageType.None); return; }
            foreach (var p in all)
            {
                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    EditorGUILayout.LabelField(p.presetName, GUILayout.Width(140));
                    EditorGUILayout.LabelField($"目录:{p.directories.Count} 包:{p.packages.Count} 插件:{p.plugins?.Count ?? 0} 设置:{p.settings.Count}", EditorStyles.miniLabel);
                    if (GUILayout.Button("编辑", GUILayout.Width(50)))
                        LoadPreset(p, false);
                }
            }
        }

        #endregion

        private void MarkDirty() => EditorUtility.SetDirty(_preset);
    }
}
#endif
