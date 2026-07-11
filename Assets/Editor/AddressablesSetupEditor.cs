using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace EditorTools
{
    /// <summary>
    /// Addressables 首次设置工具（适配 Addressables 2.x）。
    ///
    /// 自动完成以下操作：
    /// 1. 创建 AddressableAssetSettings（如果不存在）
    /// 2. 配置 Profile 的 Remote.BuildPath 和 Remote.LoadPath
    ///    - BuildPath: ServerData/[BuildTarget]
    ///    - LoadPath:  http://localhost:8080/Addressables/[BuildTarget]
    /// 3. 确保 Default Group 存在并配置为远程打包
    ///
    /// 菜单：HotUpdate/Addressables/0. Setup Addressables Settings
    /// </summary>
    public static class AddressablesSetupEditor
    {
        private const string MenuRoot = "HotUpdate/Addressables/";

        /// <summary>远程加载根 URL（与 serve_hotupdate.py 配合）</summary>
        private const string RemoteLoadPathValue = "http://localhost:8080/Addressables/[BuildTarget]";

        /// <summary>远程构建输出目录</summary>
        private const string RemoteBuildPathValue = "ServerData/[BuildTarget]";

        /// <summary>Profile 变量名（Addressables 内置约定）</summary>
        private const string RemoteBuildPathVar = "Remote.BuildPath";
        private const string RemoteLoadPathVar = "Remote.LoadPath";

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
                    settings = CreateSettingsManually();
                }

                Debug.Log($"Settings 路径: {AssetDatabase.GetAssetPath(settings)}");

                // 2. 配置 Profile
                ConfigureProfile(settings);

                // 3. 确保 Group 存在并配置 Schema
                EnsureDefaultGroup(settings);

                // 4. 开启远程 Catalog
                settings.BuildRemoteCatalog = true;
                // RemoteCatalogBuildPath / RemoteCatalogLoadPath 是 ProfileValueReference，
                // 2.x 中通过 SetVariableByName 指向 Profile 变量。
                settings.RemoteCatalogBuildPath.SetVariableByName(settings, RemoteBuildPathVar);
                settings.RemoteCatalogLoadPath.SetVariableByName(settings, RemoteLoadPathVar);

                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();

                Debug.Log("===== Addressables 设置完成 =====");
                EditorUtility.DisplayDialog("设置完成",
                    "Addressables 环境配置完成！\n\n" +
                    $"远程构建目录: {RemoteBuildPathValue}\n" +
                    $"远程加载URL: {RemoteLoadPathValue}\n\n" +
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

            var profileId = settings.activeProfileId;
            var profileName = string.IsNullOrEmpty(profileId)
                ? "(无)"
                : settings.profileSettings.GetProfileName(profileId);
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

            // 获取或创建当前活跃 Profile（2.x 用 activeProfileId 替代 activeProfile）
            var profileId = settings.activeProfileId;
            if (string.IsNullOrEmpty(profileId))
            {
                // 查找名为 "Default" 的 Profile
                var defaultData = profileSettings.GetProfileDataByName("Default");
                if (defaultData == null)
                {
                    profileId = profileSettings.AddProfile("Default", null);
                }
                else
                {
                    profileId = defaultData.Id;
                }

                settings.activeProfileId = profileId;
            }

            // 确保 Remote 变量存在
            EnsureProfileVariable(profileSettings, RemoteBuildPathVar, RemoteBuildPathValue);
            EnsureProfileVariable(profileSettings, RemoteLoadPathVar, RemoteLoadPathValue);

            // 设置当前 Profile 下这些变量的值
            profileSettings.SetValue(profileId, RemoteBuildPathVar, RemoteBuildPathValue);
            profileSettings.SetValue(profileId, RemoteLoadPathVar, RemoteLoadPathValue);

            var profileName = profileSettings.GetProfileName(profileId);
            Debug.Log($"Profile '{profileName}' 配置完成");
            Debug.Log($"  {RemoteBuildPathVar} = {RemoteBuildPathValue}");
            Debug.Log($"  {RemoteLoadPathVar} = {RemoteLoadPathValue}");
        }

        /// <summary>
        /// 确保某个 Profile 变量定义存在（不存在则创建）。
        /// 2.x 中 CreateValue 返回 variableId。
        /// </summary>
        private static void EnsureProfileVariable(AddressableAssetProfileSettings profileSettings,
            string varName, string defaultValue)
        {
            var data = profileSettings.GetProfileDataByName(varName);
            if (data == null)
            {
                profileSettings.CreateValue(varName, defaultValue);
            }
        }

        /// <summary>
        /// 确保默认 Group 存在，并配置 BundledAssetGroupSchema 为远程打包。
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

            // 添加/获取 BundledAssetGroupSchema（2.x 中在 GroupSchemas 命名空间）
            var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundleSchema == null)
            {
                bundleSchema = group.AddSchema<BundledAssetGroupSchema>();
            }

            bundleSchema.IncludeInBuild = true;
            // 2.x 中 BuildPath / LoadPath 是 ProfileValueReference，通过 SetVariableByName 设置
            bundleSchema.BuildPath.SetVariableByName(settings, RemoteBuildPathVar);
            bundleSchema.LoadPath.SetVariableByName(settings, RemoteLoadPathVar);
            bundleSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundleSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
        }
    }
}
