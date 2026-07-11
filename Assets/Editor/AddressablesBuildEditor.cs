using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Addressables 资源打包工具。
    ///
    /// 自动完成以下操作：
    /// 1. 将 DemoCube 预制体和 MainMenu 场景标记为 Addressable
    ///    - DemoCube → 地址 "DemoCube"
    ///    - MainMenu → 地址 "MainMenu"
    /// 2. 构建 Addressables（New Build → Packed Mode）
    /// 3. 拷贝构建产物到 Python 服务器目录
    ///    ServerData/[BuildTarget] → HybridCLRData/ServerOutput/Addressables/[BuildTarget]
    ///
    /// 菜单：
    /// - HotUpdate/Addressables/2. Build Addressables (一键打包资源)
    /// - HotUpdate/Addressables/3. Mark Assets as Addressable
    /// - HotUpdate/Addressables/4. Clean Build Cache
    /// </summary>
    public static class AddressablesBuildEditor
    {
        private const string MenuRoot = "HotUpdate/Addressables/";

        // ── 资源路径常量 ──────────────────────────────────────────

        /// <summary>DemoCube 预制体路径</summary>
        private const string DemoCubePath = "Assets/AddressableAssets/Prefabs/DemoCube.prefab";

        /// <summary>MainMenu 场景路径</summary>
        private const string MainMenuScenePath = "Assets/AddressableAssets/Scenes/MainMenu.unity";

        /// <summary>DemoCube 的 Addressable 地址</summary>
        private const string DemoCubeAddress = "DemoCube";

        /// <summary>MainMenu 的 Addressable 地址</summary>
        private const string MainMenuAddress = "MainMenu";

        /// <summary>默认 Group 名称</summary>
        private const string DefaultGroupName = "Default Local Group";

        // ── 服务器输出目录 ────────────────────────────────────────

        /// <summary>Python 服务器输出根目录</summary>
        private static string ServerOutputDir =>
            Path.Combine(Directory.GetCurrentDirectory(), "HybridCLRData", "ServerOutput");

        /// <summary>Addressables 在服务器输出中的子目录</summary>
        private static string ServerAddressablesDir =>
            Path.Combine(ServerOutputDir, "Addressables");

        // ── 菜单项 ────────────────────────────────────────────────

        /// <summary>
        /// 一键打包：标记资源 → 构建 → 拷贝到服务器目录。
        /// </summary>
        [MenuItem(MenuRoot + "2. Build Addressables (一键打包资源)", priority = 10)]
        public static void BuildAddressables()
        {
            try
            {
                Debug.Log("===== 开始 Addressables 资源打包 =====");

                // 0. 检查 Settings
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null)
                {
                    throw new InvalidOperationException(
                        "Addressables 未初始化，请先执行菜单 HotUpdate/Addressables/0. Setup Addressables Settings");
                }

                // 1. 标记资源
                Debug.Log("[1/3] 标记 Addressable 资源");
                MarkAssetsAsAddressable(settings);

                // 2. 构建
                Debug.Log("[2/3] 构建 Addressables");
                BuildAddressablesInternal(settings);

                // 3. 拷贝到服务器目录
                Debug.Log("[3/3] 拷贝构建产物到服务器目录");
                CopyBuildOutputToServer();

                AssetDatabase.Refresh();

                Debug.Log("===== Addressables 资源打包完成 =====");
                Debug.Log($"服务器目录: {ServerAddressablesDir}");
                EditorUtility.DisplayDialog("打包完成",
                    "Addressables 资源打包完成！\n\n" +
                    "服务器目录:\n" + ServerAddressablesDir + "\n\n" +
                    "运行 python3 tools/serve_hotupdate.py 启动本地服务器", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"Addressables 打包失败: {e}");
                EditorUtility.DisplayDialog("打包失败", e.Message, "OK");
            }
        }

        /// <summary>
        /// 将 DemoCube 和 MainMenu 标记为 Addressable。
        /// </summary>
        [MenuItem(MenuRoot + "3. Mark Assets as Addressable", priority = 11)]
        public static void MarkAssetsMenuItem()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                EditorUtility.DisplayDialog("错误",
                    "Addressables 未初始化，请先执行 Setup。", "OK");
                return;
            }

            MarkAssetsAsAddressable(settings);
            EditorUtility.DisplayDialog("完成", "资源已标记为 Addressable", "OK");
        }

        /// <summary>
        /// 清理构建缓存（解决增量构建问题）。
        /// </summary>
        [MenuItem(MenuRoot + "4. Clean Build Cache", priority = 12)]
        public static void CleanBuildCache()
        {
            try
            {
                AddressableAssetSettings.CleanPlayerContent(
                    AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder);
                Debug.Log("构建缓存已清理");
            }
            catch (Exception e)
            {
                Debug.LogError($"清理失败: {e}");
            }
        }

        // ── 内部方法 ──────────────────────────────────────────────

        /// <summary>
        /// 标记资源为 Addressable。
        /// </summary>
        private static void MarkAssetsAsAddressable(AddressableAssetSettings settings)
        {
            var group = settings.FindGroup(DefaultGroupName);
            if (group == null)
            {
                group = settings.CreateGroup(DefaultGroupName, false, false, true, null);
            }

            // 标记 DemoCube
            MarkAsset(settings, group, DemoCubePath, DemoCubeAddress);

            // 标记 MainMenu 场景
            MarkAsset(settings, group, MainMenuScenePath, MainMenuAddress);

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 标记单个资源为 Addressable。
        /// </summary>
        private static void MarkAsset(AddressableAssetSettings settings,
            AddressableAssetGroup group, string assetPath, string address)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning($"资源不存在，跳过: {assetPath}");
                return;
            }

            var entry = settings.CreateOrMoveEntry(guid, group, false);
            entry.address = address;
            Debug.Log($"标记 Addressable: {address} → {assetPath}");
        }

        /// <summary>
        /// 执行 Addressables 构建（适配 2.x API）。
        /// 2.x 中使用 AddressableAssetSettings.BuildPlayerContent() 静态方法。
        /// </summary>
        private static void BuildAddressablesInternal(AddressableAssetSettings settings)
        {
            // 2.x 的官方构建入口：BuildPlayerContent(out result)
            AddressableAssetSettings.BuildPlayerContent(out var result);

            if (result == null)
            {
                throw new Exception("构建结果为空");
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new Exception($"构建错误: {result.Error}");
            }

            Debug.Log($"Addressables 构建完成，输出目录: {result.OutputPath}");
        }

        /// <summary>
        /// 拷贝 ServerData/[BuildTarget] 到服务器输出目录。
        /// </summary>
        private static void CopyBuildOutputToServer()
        {
            var buildTarget = GetCurrentPlatformName();
            var sourceDir = Path.Combine(Directory.GetCurrentDirectory(), "ServerData", buildTarget);

            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException(
                    $"Addressables 构建输出目录不存在: {sourceDir}\n" +
                    "请确认构建步骤成功执行。");
            }

            // 目标：HybridCLRData/ServerOutput/Addressables/[BuildTarget]/
            var destDir = Path.Combine(ServerAddressablesDir, buildTarget);

            // 清空目标目录
            if (Directory.Exists(destDir))
            {
                Directory.Delete(destDir, true);
            }

            Directory.CreateDirectory(destDir);

            // 拷贝所有文件
            CopyDirectory(sourceDir, destDir);

            // 列出拷贝的文件
            var files = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories);
            Debug.Log($"已拷贝 {files.Length} 个文件到 {destDir}");
            foreach (var f in files)
            {
                var relPath = Path.GetRelativePath(destDir, f);
                var sizeKB = new FileInfo(f).Length / 1024.0;
                Debug.Log($"  {relPath} ({sizeKB:F1} KB)");
            }
        }

        /// <summary>
        /// 递归拷贝目录。
        /// </summary>
        private static void CopyDirectory(string source, string dest)
        {
            // 拷贝文件
            foreach (var file in Directory.GetFiles(source))
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(dest, fileName), true);
            }

            // 递归拷贝子目录
            foreach (var dir in Directory.GetDirectories(source))
            {
                var dirName = Path.GetFileName(dir);
                var destSubDir = Path.Combine(dest, dirName);
                Directory.CreateDirectory(destSubDir);
                CopyDirectory(dir, destSubDir);
            }
        }

        /// <summary>
        /// 获取当前平台名称（与 Addressables Profile 的 [BuildTarget] 对应）。
        /// </summary>
        private static string GetCurrentPlatformName()
        {
            return EditorUserBuildSettings.activeBuildTarget switch
            {
                BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 => "Windows",
                BuildTarget.StandaloneOSX => "OSX",
                BuildTarget.StandaloneLinux64 => "Linux",
                BuildTarget.Android => "Android",
                BuildTarget.iOS => "iOS",
                BuildTarget.WebGL => "WebGL",
                _ => EditorUserBuildSettings.activeBuildTarget.ToString()
            };
        }
    }
}
