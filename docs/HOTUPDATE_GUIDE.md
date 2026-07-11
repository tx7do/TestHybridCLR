# 热更新完整使用指南

本项目使用 **HybridCLR** 实现代码热更新。本文档描述从打包到运行的完整闭环流程。

## 架构总览

```
[Editor 打包]                              [运行时]
  HotUpdate/1. 一键打包                      GameBootstrap.Start()
    ├─ Generate/All (生成 link/AOT引用)        ├─ HybridClrManager.Initialize()  加载AOT元数据
    ├─ CompileDll (编译热更DLL)                ├─ CodeUpdateManager.CheckForUpdates()  检查版本
    ├─ 拷贝AOT → StreamingAssets/AOT/         ├─ 有更新? DownloadAndApplyUpdates()  下载+MD5校验
    ├─ 拷贝热更DLL → StreamingAssets/          └─ HotUpdateLoader.LoadAndRun()  加载DLL+执行入口
    └─ 生成 ServerOutput/manifest.json                 ↓
         ↓                                          Hello.Run() → "Hello, World"
  [Python serve ServerOutput/]
  http://localhost:8080
```

## 首次环境准备（一次性）

### 1. 初始化 HybridCLR 子模块

```bash
cd <项目根目录>
git submodule update --init --recursive
```

> 如果子模块为空，也可以在 Unity 中通过 `HybridCLR/Installer` 安装。

### 2. 在 Unity 中安装 HybridCLR

打开 Unity 项目，等待包管理器拉取 `com.code-philosophy.hybridclr`。

打开 `HybridCLR/Installer...`，确认安装状态为 `Installed`。
（macOS 用户如发现本地 il2cpp 目录名是 `LocalIl2CppData-WindowsEditor`，需重新安装一次）

### 3. Player Settings

`File/Build Settings` 中将 Scripting Backend 改为 **IL2CPP**（HybridCLR 依赖 IL2CPP）。

---

## 日常打包流程

### 步骤 1：一键打包

在 Unity 菜单栏点击：

```
HotUpdate / 1. Build HotUpdate Package (一键打包)
```

该菜单会自动执行：
1. `HybridCLR Generate/All` — 生成 link.xml、AOT 泛型引用、方法桥接
2. 编译热更 DLL → `HybridCLRData/HotUpdateDlls/HotUpdate.dll`
3. 拷贝 AOT 元数据 → `Assets/StreamingAssets/AOT/`
4. 拷贝热更 DLL → `Assets/StreamingAssets/HotUpdate.dll`（内置回退版本）
5. 生成服务器输出 → `HybridCLRData/ServerOutput/`（manifest.json + HotUpdate.dll）

打包完成后会弹出对话框提示。

### 步骤 2：启动本地服务器

```bash
cd <项目根目录>
python3 tools/serve_hotupdate.py
```

输出示例：
```
  访问地址:
    本机:   http://localhost:8080
    本机IP: http://192.168.1.100:8080  (真机测试用)

  可用文件:
    /HotUpdate.dll  (12.3 KB)
    /manifest.json  (0.2 KB)
```

> 真机测试时，手机需与电脑在同一局域网，使用「本机IP」地址。
> 需修改 `CodeUpdateManager.cs` 中的 `ServerBaseUrl` 为本机 IP。

### 步骤 3：运行测试

#### Editor 测试

直接 Play 场景。屏幕上（ConsoleToScreen）会显示完整流程日志：

```
[GameBootstrap] 启动热更新流程
[GameBootstrap] ① 初始化 HybridCLR
[GameBootstrap] ② 检查代码更新
[GameBootstrap] 版本对比: 本地=, 远程=20260711143000, 有更新=True
[GameBootstrap] 发现新版本，开始下载
[GameBootstrap] 下载进度: 10%
...
[GameBootstrap] 下载成功
[GameBootstrap] ③ 加载热更代码并执行入口
Hello, World
[GameBootstrap] 热更新流程结束
```

#### 真机测试

1. 执行 `File/Build And Run`
2. 首次安装时无本地版本，会自动下载热更 DLL
3. 再次启动时如果服务器版本未变，会显示「已是最新版本」

---

## 验证热更新生效

1. 修改 `Assets/Scripts/HotUpdate/Hello.cs` 中的日志文本：
   ```csharp
   Debug.Log("Hello, World - 已热更新!");  // 改这一行
   ```
2. 重新执行 `HotUpdate/1. 一键打包`
3. 重启 Python 服务器
4. 启动客户端 → 会重新下载新版 DLL → 屏幕显示新文本

---

## 菜单命令一览

| 菜单 | 说明 |
|------|------|
| `HotUpdate/1. Build HotUpdate Package` | **一键打包**（日常使用） |
| `HotUpdate/2. HybridCLR Generate/All` | 仅执行 HybridCLR 生成 |
| `HotUpdate/3. Compile HotUpdate DLL` | 仅编译热更 DLL |
| `HotUpdate/4. Copy AOT to StreamingAssets` | 仅拷贝 AOT 元数据 |
| `HotUpdate/5. Copy HotUpdate DLL to StreamingAssets` | 仅拷贝热更 DLL |
| `HotUpdate/6. Generate Server Output` | 仅生成服务器输出 |
| `HotUpdate/7. Open Server Output Folder` | 打开输出目录 |

---

## 文件结构说明

```
项目根/
├── Assets/
│   ├── Editor/
│   │   └── HotUpdateBuildEditor.cs      ← 打包工具
│   ├── StreamingAssets/                  ← (打包生成，不入版本库)
│   │   ├── AOT/                          ← AOT 补充元数据
│   │   │   ├── mscorlib.dll
│   │   │   ├── ...
│   │   │   └── aot_files.txt            ← Android 清单
│   │   └── HotUpdate.dll                ← 内置热更DLL（回退用）
│   ├── Scripts/
│   │   ├── Main/
│   │   │   ├── Bootstrap/
│   │   │   │   └── GameBootstrap.cs     ← 游戏入口（挂载到场景）
│   │   │   ├── HybridCLR/
│   │   │   │   ├── HybridClrManager.cs  ← AOT元数据加载
│   │   │   │   └── CodeUpdateManager.cs ← 版本检查+下载
│   │   │   ├── HotUpdateLoader.cs       ← DLL加载+入口执行
│   │   │   └── VersionManifest.cs       ← 版本清单结构
│   │   ├── HotUpdate/
│   │   │   ├── Hello.cs                 ← 热更入口类
│   │   │   └── Print.cs                 ← 热更测试组件
│   │   └── Common/Utilities/
│   │       ├── CryptoUtils.cs           ← MD5工具
│   │       └── PlatformUtils.cs         ← 平台工具
│   └── AddressableAssets/Scenes/
│       └── SampleScene.unity            ← 主场景（挂载GameBootstrap）
├── HybridCLRData/
│   ├── HotUpdateDlls/                   ← HybridCLR编译输出
│   ├── AssembliesPostIl2CppStrip/       ← 裁剪后AOT DLL
│   └── ServerOutput/                    ← (打包生成) Python serve 此目录
│       ├── manifest.json
│       └── HotUpdate.dll
├── tools/
│   └── serve_hotupdate.py               ← 本地HTTP服务器
└── docs/
    └── HOTUPDATE_GUIDE.md               ← 本文档
```

---

## 常见问题

### Q: Editor 下运行报错 "未找到 HotUpdate 程序集"

确认 `HotUpdate.asmdef` 存在且 `Hello.cs` 能正常编译。Editor 下热更代码会随项目一起编译。

### Q: 真机报错 "加载 AOT 元数据失败"

AOT 元数据需要在打包时生成。确认执行了 `HotUpdate/1. 一键打包`，且 `StreamingAssets/AOT/` 目录下有 `.dll` 文件。

### Q: 下载失败 "无法连接到服务器"

- 确认 Python 服务器正在运行
- 确认 `CodeUpdateManager.cs` 中 `ServerBaseUrl` 地址正确
- 真机测试时手机和电脑需在同一局域网，使用电脑的局域网 IP

### Q: 如何切换到正式 CDN

修改 `Assets/Scripts/Main/HybridCLR/CodeUpdateManager.cs`：

```csharp
private const string ServerBaseUrl = "https://your-cdn.example.com/hotupdate";
```

然后将 `HybridCLRData/ServerOutput/` 下的文件上传到 CDN 对应路径即可。

### Q: 如何在 macOS 上重新安装 HybridCLR

当前项目 il2cpp 目录名可能为 `LocalIl2CppData-WindowsEditor`（在 Windows 上初始化的）。
macOS 上需：`HybridCLR/Installer...` → 重新安装。
