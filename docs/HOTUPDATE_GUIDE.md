# 热更新完整使用指南

本项目使用 **HybridCLR** 实现代码热更新，**Addressables** 实现资源热更新。
本文档描述从打包到运行的完整闭环流程。

---

## 架构总览

```
┌─ SampleScene (Bootstrap场景，打进主包) ──────────────────────────────┐
│  GameBootstrap                                                        │
│    ① HybridCLR 初始化（加载 AOT 补充元数据）                           │
│    ② 检查代码更新 → 下载热更 DLL + MD5 校验                            │
│    ③ 加载热更代码入口（Hello.Run）                                      │
│    ④ Addressables 初始化                                                │
│    ⑤ 检查 Catalog 更新 → 下载资源更新                                   │
│    ⑥ Addressables.LoadSceneAsync("MainMenu")  ←远程加载游戏场景         │
└────────────────────────────────────────────────────────────────────────┘
                              ↓
┌─ MainMenu场景 (Addressable远程资源) ──────────────────────────────────┐
│  DemoSceneLoader                                                       │
│    → Addressables.InstantiateAsync("DemoCube")                        │
│      → DemoCube 上挂载 DemoCubeController（热更程序集组件）              │
│        → 旋转 + 浮动动画，验证「代码热更 + 资源热更」协同               │
└────────────────────────────────────────────────────────────────────────┘
```

### 两套热更新系统

| 系统 | 负责内容 | 传输方式 | 版本控制 |
|------|---------|---------|---------|
| **HybridCLR 代码热更** | C# DLL（逻辑代码） | HttpClient 下载 + Assembly.Load | manifest.json 版本对比 |
| **Addressables 资源热更** | 预制体、场景、贴图等 | Addressables 远程加载 | Catalog 自动版本管理 |

---

## 首次环境准备（一次性）

### 1. 初始化 HybridCLR 子模块

```bash
cd <项目根目录>
git submodule update --init --recursive
```

> 也可在 Unity 中通过 `HybridCLR/Installer` 安装。

### 2. 打开 Unity，等待包加载

确认以下包已导入：
- `HybridCLR`（代码热更）
- `Addressables 1.22.3`（资源热更）

### 3. 安装 HybridCLR

`HybridCLR/Installer...` → 确认状态为 `Installed`。

### 4. Player Settings

`File/Build Settings` → Scripting Backend 改为 **IL2CPP**。

### 5. 初始化 Addressables（仅需一次）

执行菜单：

```
HotUpdate / Addressables / 0. Setup Addressables Settings
```

这会自动：
- 创建 `AddressableAssetSettings`
- 配置 Profile（Remote.LoadPath = `http://localhost:8080/Addressables/[BuildTarget]`）
- 创建默认 Group 并配置为远程打包

---

## 日常打包流程

### 完整打包（代码 + 资源）

#### 步骤 1：代码热更打包

```
HotUpdate / 1. Build HotUpdate Package (一键打包)
```

产出：`HybridCLRData/ServerOutput/`（manifest.json + HotUpdate.dll）

#### 步骤 2：资源热更打包

```
HotUpdate / Addressables / 2. Build Addressables (一键打包资源)
```

这会自动：
1. 将 DemoCube 预制体和 MainMenu 场景标记为 Addressable
2. 构建 Addressables（生成 catalog + bundle）
3. 拷贝到 `HybridCLRData/ServerOutput/Addressables/[平台]/`

#### 步骤 3：启动服务器

```bash
cd <项目根目录>
python3 tools/serve_hotupdate.py
```

输出示例：
```
  ── 代码热更新 ──
    /HotUpdate.dll  (12.3 KB)
    /manifest.json  (0.2 KB)

  ── 资源热更新 (Addressables) ──
    /Addressables/OSX/
      /catalog_2026...json  (1.5 KB)
      /catalog_2026...hash  (32 B)
      /mainmenu_all.bundle  (15.2 KB)
```

#### 步骤 4：运行测试

Play 场景，屏幕日志（ConsoleToScreen）会显示完整流程：

```
[GameBootstrap] ① 初始化 HybridCLR
[GameBootstrap] ② 检查代码更新
[GameBootstrap] ③ 加载热更代码并执行入口
Hello, World
[GameBootstrap] ④ 初始化 Addressables
[GameBootstrap] ⑤ 检查资源更新
[GameBootstrap] ⑥ 加载游戏场景: MainMenu
[DemoSceneLoader] DemoCube 实例化成功
[DemoCube] 热更组件启动，旋转速度=90°/s
```

---

## 验证热更新

### 验证代码热更

1. 修改 `Hello.cs` 的日志文本
2. 执行 `HotUpdate/1. Build HotUpdate Package`
3. 重启 Python 服务器
4. 启动客户端 → 自动下载新 DLL → 显示新文本

### 验证资源热更

1. 修改 `DemoCube.prefab` 的颜色/大小/旋转参数
   （或在 `DemoCubeController.cs` 改 `rotateSpeed`）
2. 执行 `HotUpdate/Addressables/2. Build Addressables`
3. 重启 Python 服务器
4. 启动客户端 → 自动下载新资源 → 观察新效果

---

## 菜单命令一览

### 代码热更新

| 菜单 | 说明 |
|------|------|
| `HotUpdate/1. Build HotUpdate Package` | 一键代码打包 |
| `HotUpdate/2-5. ...` | 分步代码打包（高级） |

### 资源热更新

| 菜单 | 说明 |
|------|------|
| `HotUpdate/Addressables/0. Setup Addressables Settings` | **首次初始化**（仅1次） |
| `HotUpdate/Addressables/1. Check Addressables Status` | 检查配置状态 |
| `HotUpdate/Addressables/2. Build Addressables` | **一键资源打包** |
| `HotUpdate/Addressables/3. Mark Assets as Addressable` | 仅标记资源 |
| `HotUpdate/Addressables/4. Clean Build Cache` | 清理构建缓存 |

---

## 文件结构说明

```
项目根/
├── Assets/
│   ├── Editor/
│   │   ├── HotUpdateBuildEditor.cs       ← 代码打包工具
│   │   ├── AddressablesSetupEditor.cs    ← Addressables初始化工具
│   │   └── AddressablesBuildEditor.cs    ← Addressables打包工具
│   ├── AddressableAssets/
│   │   ├── Prefabs/
│   │   │   └── DemoCube.prefab           ← 示例预制体(Addressable)
│   │   └── Scenes/
│   │       ├── SampleScene.unity         ← Bootstrap场景(打进主包)
│   │       └── MainMenu.unity            ← 游戏场景(Addressable远程)
│   ├── Scripts/
│   │   ├── Main/                         ← 非热更代码(Assembly-CSharp)
│   │   │   ├── Bootstrap/GameBootstrap.cs
│   │   │   ├── HybridCLR/
│   │   │   ├── HotUpdateLoader.cs
│   │   │   ├── DemoSceneLoader.cs        ← MainMenu场景的加载器
│   │   │   └── VersionManifest.cs
│   │   ├── HotUpdate/                    ← 热更代码
│   │   │   ├── Hello.cs                  ← 代码入口
│   │   │   ├── DemoCubeController.cs     ← 预制体上的热更组件
│   │   │   └── HotUpdate.asmdef
│   │   └── Common/Utilities/
│   ├── AddressableAssetsData/            ← Addressables配置(Setup生成)
│   │   ├── AddressableAssetSettings.asset
│   │   ├── DefaultObject.asset
│   │   └── AssetGroups/                  ← 入版本库
│   └── StreamingAssets/                  ← (打包生成)
├── ServerData/                           ← Addressables构建输出(打包生成)
├── HybridCLRData/
│   └── ServerOutput/                     ← Python serve此目录
│       ├── manifest.json                 ← 代码热更清单
│       ├── HotUpdate.dll                ← 代码热更DLL
│       └── Addressables/[平台]/          ← 资源热更产物
├── tools/
│   └── serve_hotupdate.py
└── docs/
    └── HOTUPDATE_GUIDE.md
```

---

## 关键配置说明

### Addressables Profile（由 Setup 脚本自动配置）

| 变量 | 值 | 说明 |
|------|-----|------|
| `Remote.BuildPath` | `ServerData/[BuildTarget]` | 构建输出目录 |
| `Remote.LoadPath` | `http://localhost:8080/Addressables/[BuildTarget]` | 运行时加载URL |

### Addressable 地址映射

| 资源 | 地址 | 用途 |
|------|------|------|
| `DemoCube.prefab` | `DemoCube` | DemoSceneLoader 实例化 |
| `MainMenu.unity` | `MainMenu` | GameBootstrap.LoadSceneAsync |

### 切换到正式 CDN

**代码热更**：修改 `CodeUpdateManager.cs`：
```csharp
private const string ServerBaseUrl = "https://your-cdn.com/hotupdate";
```

**资源热更**：修改 Addressables Profile 的 `Remote.LoadPath`：
```
https://your-cdn.com/addressables/[BuildTarget]
```

然后将 `ServerOutput/` 下所有文件上传到对应 CDN 路径。

---

## 常见问题

### Q: Editor 下 Addressables 报 "catalog not found"

首次必须执行 `HotUpdate/Addressables/2. Build Addressables` 生成 catalog。
Editor 下 Addressables 需要有构建产物才能加载。

### Q: Build Addressables 报 "Settings not found"

先执行 `HotUpdate/Addressables/0. Setup Addressables Settings`。

### Q: 增量构建后资源没更新

执行 `HotUpdate/Addressables/4. Clean Build Cache` 后重新打包。

### Q: 真机无法连接服务器

- 确认 Python 服务器在运行
- 真机使用电脑的局域网 IP（Python 启动时会打印）
- 修改 `CodeUpdateManager.cs` 的 `ServerBaseUrl`
- 修改 Addressables Profile 的 `Remote.LoadPath`

### Q: DemoCube 加载后看不到

- 检查摄像机位置和朝向（MainMenu 的 Main Camera 在原点）
- DemoCube 实例化在 z=5 位置，摄像机应该能看到
- 查看 Console 中是否有错误日志
