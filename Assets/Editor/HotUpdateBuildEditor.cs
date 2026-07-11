using System;
using System.Collections.Generic;
using System.IO;
using HybridCLR.Editor.Commands;
using Main;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// 热更新一键打包工具。
    ///
    /// 提供菜单：
    /// - <see cref="BuildHotUpdatePackage"/>: 编译热更 DLL → 拷贝到 StreamingAssets → 生成服务器输出
    /// - <see cref="GenerateHybridCLRAll"/>: 执行 HybridCLR Generate/All（生成 link.xml、AOT 引用、方法桥接）
    /// - <see cref="CompileHotUpdateDll"/>: 仅编译热更 DLL
    /// - <see cref="CopyAOTToStreamingAssets"/>: 仅拷贝 AOT 元数据到 StreamingAssets
    /// - <see cref="OpenServerOutputFolder"/>: 打开服务器输出目录
    ///
    /// 目录约定：
    /// - 热更 DLL 输入: HybridCLRData/HotUpdateDlls/HotUpdate.dll
    /// - AOT DLL 输入: HybridCLRData/AssembliesPostIl2CppStrip/[平台]/
    /// - AOT DLL 输出: Assets/StreamingAssets/AOT/
    /// - 服务器输出:   HybridCLRData/ServerOutput/（供 Python serve）
    /// </summary>
    public static class HotUpdateBuildEditor
    {
        private const string MenuRoot = "HotUpdate/";

        // ── 路径常量 ──────────────────────────────────────────────

        /// <summary>HybridCLR 编译热更 DLL 的输出目录</summary>
        private static string HotUpdateDllSourceDir =>
            Path.Combine(Directory.GetCurrentDirectory(), "HybridCLRData", "HotUpdateDlls");

        /// <summary>HybridCLR 裁剪后 AOT DLL 的输出根目录</summary>
        private static string AOTDllSourceRootDir =>
            Path.Combine(Directory.GetCurrentDirectory(), "HybridCLRData", "AssembliesPostIl2CppStrip");

        /// <summary>StreamingAssets/AOT 目录（随主包打入）</summary>
        private static string StreamingAOTDir =>
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "StreamingAssets", "AOT");

        /// <summary>StreamingAssets 下热更 DLL（随主包打入，作为无更新时的回退）</summary>
        private static string StreamingHotUpdateDll =>
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "StreamingAssets", "HotUpdate.dll");

        /// <summary>服务器输出目录（Python serve 此目录）</summary>
        private static string ServerOutputDir =>
            Path.Combine(Directory.GetCurrentDirectory(), "HybridCLRData", "ServerOutput");

        /// <summary>热更程序集 DLL 文件名</summary>
        private const string HotUpdateDllName = "HotUpdate.dll";

        // ── 菜单项 ────────────────────────────────────────────────

        /// <summary>
        /// 一键打包：编译 DLL → 生成 AOT → 拷贝 → 生成 manifest。
        /// 这是日常使用的唯一入口。
        /// </summary>
        [MenuItem(MenuRoot + "1. Build HotUpdate Package (一键打包)")]
        public static void BuildHotUpdatePackage()
        {
            try
            {
                Debug.Log("===== 开始热更新打包 =====");

                // 1. 生成 HybridCLR 必要文件（link.xml、AOT 引用、方法桥接）
                Debug.Log("[1/5] 执行 HybridCLR Generate/All");
                GenerateHybridCLRAll();

                // 2. 编译热更 DLL
                Debug.Log("[2/5] 编译热更 DLL");
                CompileHotUpdateDll();

                // 3. 拷贝 AOT 元数据到 StreamingAssets
                Debug.Log("[3/5] 拷贝 AOT 元数据到 StreamingAssets");
                CopyAOTToStreamingAssets();

                // 4. 拷贝热更 DLL 到 StreamingAssets（内置回退版本）
                Debug.Log("[4/5] 拷贝热更 DLL 到 StreamingAssets");
                CopyHotUpdateDllToStreamingAssets();

                // 5. 生成服务器输出（热更 DLL + manifest.json）
                Debug.Log("[5/5] 生成服务器输出目录");
                GenerateServerOutput();

                AssetDatabase.Refresh();

                Debug.Log("===== 热更新打包完成 =====");
                Debug.Log($"服务器输出目录: {ServerOutputDir}");
                Debug.Log("运行 `python3 tools/serve_hotupdate.py` 启动本地服务器");
                EditorUtility.DisplayDialog("打包完成",
                    "热更新打包完成！\n\n" +
                    "服务器输出目录:\n" + ServerOutputDir + "\n\n" +
                    "运行 python3 tools/serve_hotupdate.py 启动本地服务器", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"打包失败: {e}");
                EditorUtility.DisplayDialog("打包失败", e.Message, "OK");
            }
        }

        /// <summary>
        /// 执行 HybridCLR Generate/All（相当于菜单 HybridCLR/Generate/All）。
        /// </summary>
        [MenuItem(MenuRoot + "2. HybridCLR Generate/All")]
        public static void GenerateHybridCLRAll()
        {
            PrebuildCommand.GenerateAll();
            Debug.Log("HybridCLR Generate/All 完成");
        }

        /// <summary>
        /// 编译当前平台的热更 DLL。
        /// </summary>
        [MenuItem(MenuRoot + "3. Compile HotUpdate DLL")]
        public static void CompileHotUpdateDll()
        {
            CompileDllCommand.CompileDll(EditorUserBuildSettings.activeBuildTarget);
            Debug.Log($"热更 DLL 编译完成，输出目录: {HotUpdateDllSourceDir}");

            var dllPath = Path.Combine(HotUpdateDllSourceDir, HotUpdateDllName);
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException($"编译后未找到热更 DLL: {dllPath}", dllPath);
            }
        }

        /// <summary>
        /// 拷贝当前平台裁剪后的 AOT DLL 到 StreamingAssets/AOT/。
        /// </summary>
        [MenuItem(MenuRoot + "4. Copy AOT to StreamingAssets")]
        public static void CopyAOTToStreamingAssets()
        {
            var platformDir = GetPlatformSubDir();
            var aotSourceDir = Path.Combine(AOTDllSourceRootDir, platformDir);

            if (!Directory.Exists(aotSourceDir))
            {
                throw new DirectoryNotFoundException(
                    $"AOT DLL 源目录不存在: {aotSourceDir}\n" +
                    "请先执行 HybridCLR Generate/All。");
            }

            // 清空目标目录
            if (Directory.Exists(StreamingAOTDir))
            {
                Directory.Delete(StreamingAOTDir, true);
            }

            Directory.CreateDirectory(StreamingAOTDir);

            // 拷贝所有 .dll
            var dllFiles = Directory.GetFiles(aotSourceDir, "*.dll", SearchOption.TopDirectoryOnly);
            var aotFileNames = new List<string>();

            foreach (var dll in dllFiles)
            {
                var fileName = Path.GetFileName(dll);
                var destPath = Path.Combine(StreamingAOTDir, fileName);
                File.Copy(dll, destPath, true);
                aotFileNames.Add(fileName);
            }

            // 生成 Android 用的文件清单（Android 下 StreamingAssets 无法列举目录）
            var manifestPath = Path.Combine(StreamingAOTDir, "aot_files.txt");
            File.WriteAllLines(manifestPath, aotFileNames);

            Debug.Log($"AOT 元数据拷贝完成: {aotFileNames.Count} 个 DLL → {StreamingAOTDir}");
        }

        /// <summary>
        /// 拷贝热更 DLL 到 StreamingAssets（作为无网络/无更新时的内置版本）。
        /// </summary>
        [MenuItem(MenuRoot + "5. Copy HotUpdate DLL to StreamingAssets")]
        public static void CopyHotUpdateDllToStreamingAssets()
        {
            var srcDll = Path.Combine(HotUpdateDllSourceDir, HotUpdateDllName);
            if (!File.Exists(srcDll))
            {
                throw new FileNotFoundException(
                    $"热更 DLL 不存在: {srcDll}\n请先执行编译。", srcDll);
            }

            var streamingDir = Path.GetDirectoryName(StreamingHotUpdateDll);
            if (!Directory.Exists(streamingDir))
            {
                Directory.CreateDirectory(streamingDir);
            }

            File.Copy(srcDll, StreamingHotUpdateDll, true);
            Debug.Log($"热更 DLL 已拷贝到 StreamingAssets: {StreamingHotUpdateDll}");
        }

        /// <summary>
        /// 生成服务器输出目录（热更 DLL + manifest.json）。
        /// </summary>
        [MenuItem(MenuRoot + "6. Generate Server Output")]
        public static void GenerateServerOutput()
        {
            var srcDll = Path.Combine(HotUpdateDllSourceDir, HotUpdateDllName);
            if (!File.Exists(srcDll))
            {
                throw new FileNotFoundException(
                    $"热更 DLL 不存在: {srcDll}\n请先执行编译。", srcDll);
            }

            // 清空服务器输出目录
            if (Directory.Exists(ServerOutputDir))
            {
                Directory.Delete(ServerOutputDir, true);
            }

            Directory.CreateDirectory(ServerOutputDir);

            // 拷贝热更 DLL
            var destDll = Path.Combine(ServerOutputDir, HotUpdateDllName);
            File.Copy(srcDll, destDll, true);

            // 生成 manifest.json
            var dllBytes = File.ReadAllBytes(destDll);
            var md5 = ComputeMD5(dllBytes);
            var version = DateTime.Now.ToString("yyyyMMddHHmmss");

            var manifest = new VersionManifest
            {
                version = version,
                hotUpdateDlls = new List<DllEntry>
                {
                    new()
                    {
                        name = HotUpdateDllName,
                        md5 = md5,
                        size = dllBytes.Length
                    }
                }
            };

            var json = JsonUtility.ToJson(manifest, true);
            var manifestPath = Path.Combine(ServerOutputDir, "manifest.json");
            File.WriteAllText(manifestPath, json);

            Debug.Log($"manifest.json 已生成 (version={version}, md5={md5})");
        }

        /// <summary>
        /// 在 Finder/资源管理器中打开服务器输出目录。
        /// </summary>
        [MenuItem(MenuRoot + "7. Open Server Output Folder")]
        public static void OpenServerOutputFolder()
        {
            if (!Directory.Exists(ServerOutputDir))
            {
                EditorUtility.DisplayDialog("提示", "服务器输出目录不存在，请先执行打包。", "OK");
                return;
            }

            EditorUtility.RevealInFinder(ServerOutputDir);
        }

        // ── 辅助方法 ──────────────────────────────────────────────

        /// <summary>
        /// 获取当前 BuildTarget 对应的 AOT 子目录名。
        /// </summary>
        private static string GetPlatformSubDir()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            return target switch
            {
                BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 => "Windows64",
                BuildTarget.StandaloneOSX => "OSX",
                BuildTarget.StandaloneLinux64 => "Linux64",
                BuildTarget.Android => "Android",
                BuildTarget.iOS => "IOS",
                BuildTarget.WebGL => "WebGL",
                _ => target.ToString()
            };
        }

        /// <summary>
        /// 计算字节数组的 MD5（16 进制小写）。
        /// </summary>
        private static string ComputeMD5(byte[] data)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(data);
            var sb = new System.Text.StringBuilder();
            foreach (var b in hash)
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
