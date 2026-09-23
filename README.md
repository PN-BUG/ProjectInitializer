# Project Initializer

通用项目初始化工具，提供预设系统、目录模板创建、依赖包安装、项目设置应用等功能。

## 功能特性

- **预设系统** — 创建/编辑/导入项目初始化预设
- **目录模板** — 快速创建项目目录结构
- **依赖包安装** — 自动安装常用依赖包
- **插件文件复制** — 扫描 `Assets/Plugins` 的顶层目录与独立文件，逐项选择是否完整复制
- **项目设置** — 应用预设支持的 Player Settings 配置
- **跨项目读取** — 选择另一个 Unity 项目目录，导入已有预设，或直接扫描该项目生成预设
- **本地预设** — 预设和插件归档保存在当前项目的本地目录，由目录内 `.gitignore` 排除

## 包信息

| 属性 | 值 |
|------|-----|
| 包名 | `com.projectinitializer.core` |
| 版本 | 1.0.0 |
| Unity 版本 | 2022.3+ |
| 仓库地址 | https://github.com/PN-BUG/ProjectInitializer.git |

## 依赖关系

工具仅依赖 Unity Editor API，不会在加载时改写 `manifest.json`。Nodin 是可选预设依赖；已有的 `NodinSetup.EnsureNodinDependency()` 可由需要它的项目主动调用。

## 目录结构

```
ProjectInitializer/
├── package.json
├── README.md
└── Editor/
    ├── ProjectInitializer.Editor.asmdef
    ├── ProjectInitializerWindow.cs      # 主窗口
    ├── Setup/                           # 可选 Nodin 配置辅助
    │   ├── ProjectInitializer.Setup.asmdef
    │   └── NodinSetup.cs               # 按需调用，不自动改写 manifest.json
    ├── DirectoryTemplate/
    │   └── DirectoryTemplateCreator.cs  # 目录模板创建
    ├── PackageInstaller/
    │   └── PackageInstaller.cs          # 依赖包安装
    ├── PresetSystem/
    │   ├── PluginArchiveManager.cs     # 插件归档和复制
    │   ├── PresetEditorWindow.cs        # 预设编辑器
    │   ├── PresetManager.cs             # 预设管理器
    │   └── ProjectInitPreset.cs         # 预设数据类
    └── ProjectSettings/
        └── ProjectSettingsApplier.cs    # 项目设置应用
```

## 使用方式

### 1. 通过 Git URL 安装

在 Unity Package Manager 中选择 **Add package from git URL**，输入 `https://github.com/PN-BUG/ProjectInitializer.git`。也可在 `Packages/manifest.json` 中添加：

```json
{
  "dependencies": {
    "com.projectinitializer.core": "https://github.com/PN-BUG/ProjectInitializer.git"
  }
}
```

### 2. 作为本地包引入

将仓库克隆或作为子模块放在 `Packages/` 目录下：

```bash
git submodule add https://github.com/PN-BUG/ProjectInitializer.git Packages/ProjectInitializer
```

Unity 会自动识别为本地包，无需额外配置。

## 快速开始

1. 在目标 Unity 项目中通过 Git URL 安装工具，打开菜单 `Tools > 项目初始化工具`。
2. 在「从其他 Unity 项目读取」中选择源项目根目录。该目录应包含 `Assets`、`Packages/manifest.json` 和 `ProjectSettings/ProjectVersion.txt`。
3. 选择一种读取方式：
   - **读取该项目的预设**：导入源项目 `Assets/ProjectInitializer/Presets` 中已有的 `.asset` 预设及同名 `.plugins.bytes` 插件归档。也兼容旧版 `Packages/ProjectInitializer/Presets` 目录。重复导入同一文件会跳过。
   - **直接扫描项目生成预设**：读取 `Assets` 前两层目录、`Packages/manifest.json` 的直接依赖、`Assets` 中的本地包、`Assets/Plugins` 顶层插件，以及部分 Player 设置。在预设编辑器中检查条目并保存。
4. 在主窗口选择预设，检查本次要创建的目录、安装的包、复制的插件和应用的设置，再点击「一键初始化」。已有的目标插件会跳过，不会覆盖；主窗口中的勾选仅影响本次执行。

目录、依赖包、插件和设置列表支持按住 Shift 点击复选框连续勾选或取消勾选。目录树的父目录行提供「全选/全不选」，只影响该目录及其子目录；主窗口的概览列表会随窗口高度伸缩。

扫描结果是初始化清单，不会复制整个源项目。`Assets/Plugins` 中的插件可以逐项选择是否复制；保存扫描所得的预设时，工具会把插件文件及其 `.meta` 打包到同名 `.plugins.bytes` 归档。本地包的源路径可能只在源项目有效，默认不勾选安装，请在预设编辑器中检查。

## 预设文件与 Git

所有新建或导入的预设都保存在**目标项目**的 `Assets/ProjectInitializer/Presets`，不保存在 Git 安装的工具包内。该目录由工具自动创建 `.gitignore`，排除 `.asset`、`.plugins.bytes` 及其 `.meta`，因此预设和插件归档不会随项目提交。工具仓库也忽略旧版 `Presets` 目录，且不再跟踪默认预设资产；没有预设时，默认预设由代码在本地生成。

若要在另一台电脑使用本地预设，请手动复制 `.asset` 和同名 `.plugins.bytes` 到可访问的源项目，或复制到新项目的预设目录。只复制 `.asset` 时，插件复制功能没有归档可用。已经被 Git 跟踪的旧预设不会因新增 `.gitignore` 自动取消跟踪，需在对应项目仓库执行 `git rm --cached <预设文件路径>` 后提交索引变更；该命令保留磁盘上的文件。
