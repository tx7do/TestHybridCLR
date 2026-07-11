using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Addressables 首次设置工具。
    ///
    /// 自动完成以下操作：
    /// 1. 创建 AddressableAssetSettings（如果不存在）
    /// 2. 配置 Profile 的 Remote.BuildPath 和 Remote.LoadPath
    ///    - BuildPath: ServerData/[BuildTarget]
    ///    - LoadPath:  http://localhost:8080/Addressables/[BuildTarget]
    /// 3. 确保 Default Group 存在
    ///
    /// 菜单：HotUpdate/Addressables/0. Setup Addressables Settings
    /// </summary>
    public static class AddressablesSetupEditor
    {
        private const string MenuRoot = "HotUpdate/Addressables/";

        /// <summary>远程加载根 URL（与 serve_hotupdate.py 配合）</summary>
        private const string RemoteLoadUrl = "http://localhost:8080/Addressables/[BuildTarget]";

        /// <summary>远程构建输出目录</summary>
        private const string RemoteBuildPath = "ServerData/[BuildTarget]";

        /// <summary>默认 Group 名称</summary>
        private const string DefaultGroupName = "Default Local Group";

        /// <summary>
        /// 一键设置 Addressables 环境。
        /// </summary>
        [MenuItem(MenuRoot + "0. Setup Addressables Settings", priority = 0)]
        public static void SetupAddressables()
        {
            try
            {
                Debug.Log("===== 开始 Addressables 设置 =====");

                // 1. 获取或创建 Settings
                var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                if (settings == null)
                {
                    // GetSettings(true) 在极端情况下可能返回 null，手动创建
                    settings = CreateSettingsManually();
                }

                Debug.Log($"Settings 路径: {AssetDatabase.GetAssetPath(settings)}");

                // 2. 配置 Profile
                ConfigureProfile(settings);

                // 3. 确保 Group 存在
                EnsureDefaultGroup(settings);

                // 4. 设置 Build 远程目录
                settings.BuildRemoteCatalog = true;
                settings.RemoteCatalogLoadPath = RemoteLoadUrl;
                settings.SaveRemoteCatalogStartupBehavior =
                    AddressableAssetSettings.RemoteCatalogLoadHint.Enabled;

                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();

                Debug.Log("===== Addressables 设置完成 =====");
                EditorUtility.DisplayDialog("设置完成",
                    "Addressables 环境配置完成！\n\n" +
                    $"远程构建目录: {RemoteBuildPath}\n" +
                    $"远程加载URL: {RemoteLoadUrl}\n\n" +
                    "现在可以执行 Build Addressables 打包资源了。", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"Addressables 设置失败: {e}");
                EditorUtility.DisplayDialog("设置失败",
                    $"自动设置失败: {e.Message}\n\n" +
                    "请手动操作：\n" +
                    "Window > Asset Management > Addressables > Groups > Create Addressables Settings", "OK");
            }
        }

        /// <summary>
        /// 检查 Addressables 是否已初始化。
        /// </summary>
        [MenuItem(MenuRoot + "1. Check Addressables Status", priority = 1)]
        public static void CheckStatus()
        {
            var settings = AddressableAssetSettingsDefaultObject.SettingsExists
                ? AddressableAssetSettingsDefaultObject.Settings
                : null;

            if (settings == null)
            {
                EditorUtility.DisplayDialog("状态检查",
                    "Addressables 未初始化。\n请先执行 Setup Addressables Settings。", "OK");
                return;
            }

            var activeProfile = settings.activeProfile;
            var profileName = activeProfile != null ? activeProfile.name : "(无)";
            var groupCount = settings.groups.Count;

            Debug.Log($"[Addressables] Settings: {AssetDatabase.GetAssetPath(settings)}");
            Debug.Log($"[Addressables] Active Profile: {profileName}");
            Debug.Log($"[Addressables] Groups: {groupCount}");
            foreach (var group in settings.groups)
            {
                var entryCount = group.entries.Count;
                Debug.Log($"  - {group.name}: {entryCount} entries");
            }

            EditorUtility.DisplayDialog("状态检查",
                $"Profile: {profileName}\nGroups: {groupCount}\n\n详见 Console 日志。", "OK");
        }

        // ── 内部方法 ──────────────────────────────────────────────

        /// <summary>
        /// 手动创建 Settings（当 GetSettings(true) 失败时的回退方案）。
        /// </summary>
        private static AddressableAssetSettings CreateSettingsManually()
        {
            const string configFolder = "Assets/AddressableAssetsData";
            if (!Directory.Exists(configFolder))
            {
                Directory.CreateDirectory(configFolder);
            }

            var settings = AddressableAssetSettings.Create(
                configFolder,
                "AddressableAssetSettings",
                true,
                true);

            AddressableAssetSettingsDefaultObject.Settings = settings;
            return settings;
        }

        /// <summary>
        /// 配置 Profile 的 Remote 路径。
        /// </summary>
        private static void ConfigureProfile(AddressableAssetSettings settings)
        {
            var profileSettings = settings.profileSettings;

            // 获取或创建当前活跃 Profile
            var profile = settings.activeProfile;
            if (profile == null)
            {
                profile = profileSettings.GetProfile("Default");
                if (profile == null)
                {
                    profile = profileSettings.AddProfile("Default", null);
                }

                settings.activeProfileId = profile.id;
            }

            // 设置 Remote.BuildPath / Remote.LoadPath
            SetValue(profileSettings, profile.id, "Remote.BuildPath", RemoteBuildPath);
            SetValue(profileSettings, profile.id, "Remote.LoadPath", RemoteLoadUrl);

            // 同时配置 Local 路径（默认值即可）
            SetValue(profileSettings, profile.id, "Local.BuildPath", "[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]");
            SetValue(profileSettings, profile.id, "Local.LoadPath", "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]");

            Debug.Log($"Profile '{profile.name}' 配置完成");
            Debug.Log($"  Remote.BuildPath = {RemoteBuildPath}");
            Debug.Log($"  Remote.LoadPath  = {RemoteLoadUrl}");
        }

        /// <summary>
        /// 设置 Profile 变量值，如果变量不存在则创建。
        /// </summary>
        private static void SetValue(AddressableAssetProfileSettings profileSettings,
            string profileId, string varName, string value)
        {
            var varId = profileSettings.GetVariableId(varName);
            if (string.IsNullOrEmpty(varId))
            {
                // 变量不存在，创建它
                profileSettings.CreateValue(varName, value);
            }
            else
            {
                profileSettings.SetValue(profileId, varName, value);
            }
        }

        /// <summary>
        /// 确保默认 Group 存在。
        /// </summary>
        private static void EnsureDefaultGroup(AddressableAssetSettings settings)
        {
            var group = settings.FindGroup(DefaultGroupName);
            if (group == null)
            {
                group = settings.CreateGroup(DefaultGroupName, false, false, true, null);
                Debug.Log($"创建 Group: {DefaultGroupName}");
            }
            else
            {
                Debug.Log($"Group 已存在: {DefaultGroupName}");
            }

            // 将 Group 的 Bundle 模式设为 Together（打包到一个 bundle）
            // 并设置远程构建
            var schema = group.GetSchema<AddressableAssetGroupSchema>();
            if (schema != null)
            {
                schema.IncludeInBuild = true;
                schema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
                schema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");
            }

            // 添加 BundledAssetGroupSchema（用于远程打包配置）
            var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundleSchema == null)
            {
                bundleSchema = group.AddSchema<BundledAssetGroupSchema>();
            }

            bundleSchema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
            bundleSchema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");
            bundleSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundleSchema.CompressBundle = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
        }
    }
}
