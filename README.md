# Project Initializer

通用项目初始化工具，提供预设系统、目录模板创建、依赖包安装、项目设置应用等功能。

## 功能特性

- **预设系统** — 创建/编辑/导入项目初始化预设
- **目录模板** — 快速创建项目目录结构
- **依赖包安装** — 自动安装常用依赖包
- **项目设置** — 应用项目配置（Player Settings、Quality Settings 等）

## 包信息

| 属性 | 值 |
|------|-----|
| 包名 | `com.projectinitializer.core` |
| 版本 | 1.0.0 |
| Unity 版本 | 2022.3+ |
| 仓库地址 | https://github.com/PN-BUG/ProjectInitializer.git |

## 依赖关系

```
ProjectInitializer
  └── Nodin (com.zko.nodin) — 自动安装，无需手动配置
```

Nodin 依赖通过 `Editor/Setup/NodinSetup.cs` 自动处理：
- 首次加载时自动将 `com.zko.nodin` 写入 `manifest.json`
- Unity 自动解析并下载 Nodin 包
- 独立 asmdef（`ProjectInitializer.Setup`），不引用 Nodin，确保即使 Nodin 未安装也能编译

## 目录结构

```
ProjectInitializer/
├── package.json
├── README.md
├── Presets/
│   └── DefaultGameProjectPreset.asset
└── Editor/
    ├── ProjectInitializer.Editor.asmdef
    ├── ProjectInitializerWindow.cs      # 主窗口
    ├── Setup/                           # 自动配置（独立程序集，不引用 Nodin）
    │   ├── ProjectInitializer.Setup.asmdef
    │   └── NodinSetup.cs               # [InitializeOnLoad] 自动写入 manifest.json
    ├── DirectoryTemplate/
    │   └── DirectoryTemplateCreator.cs  # 目录模板创建
    ├── PackageInstaller/
    │   └── PackageInstaller.cs          # 依赖包安装
    ├── PresetSystem/
    │   ├── PresetEditorWindow.cs        # 预设编辑器
    │   ├── PresetManager.cs             # 预设管理器
    │   └── ProjectInitPreset.cs         # 预设数据类
    └── ProjectSettings/
        └── ProjectSettingsApplier.cs    # 项目设置应用
```

## 使用方式

### 1. 作为本地包引入（推荐）

将仓库克隆或作为子模块放在 `Packages/` 目录下：

```bash
git submodule add https://github.com/PN-BUG/ProjectInitializer.git Packages/ProjectInitializer
```

Unity 会自动识别为本地包，无需额外配置。

### 2. 通过 Git URL 安装

在 `Packages/manifest.json` 中添加：

```json
{
  "dependencies": {
    "com.projectinitializer.core": "https://github.com/PN-BUG/ProjectInitializer.git"
  }
}
```

### 3. 在 asmdef 中引用

```json
{
  "references": [
    "ProjectInitializer.Editor",
    "Nodin.Runtime",
    "Nodin.Editor"
  ]
}
```

## 快速开始

1. 菜单：`Window > Project Initializer`
2. 选择预设或创建新预设
3. 配置目录结构和依赖包
4. 点击"应用"初始化项目
