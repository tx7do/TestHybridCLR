using System.IO;
using HybridCLR;
using UnityEngine;
using UnityEngine.Networking;

namespace Main.HybridCLR
{
    /// <summary>
    /// HybridCLR 运行时管理器：负责加载 AOT 补充元数据程序集。
    ///
    /// AOT 元数据 DLL 随主程序打包到 StreamingAssets/AOT/ 目录，
    /// 运行时扫描该目录下的所有 .dll 文件并加载。
    ///
    /// 注意：
    /// - HybridCLR 运行时无需显式 Initialize，直接调用 LoadMetadataForAOTAssembly 即可。
    /// - Editor 下 LoadMetadataForAOTAssembly 为空实现，即使没有 AOT 文件也不会报错。
    /// </summary>
    public static class HybridClrManager
    {
        private static bool _initialized;

        /// <summary>
        /// 初始化：加载 StreamingAssets/AOT/ 下所有补充元数据。
        /// 需在主线程调用（内部使用 UnityWebRequest）。
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            LoadAOTAssemblies();
            _initialized = true;
        }

        /// <summary>
        /// 扫描并加载 AOT 元数据目录下所有 DLL。
        /// </summary>
        private static void LoadAOTAssemblies()
        {
            var aotDir = Path.Combine(Application.streamingAssetsPath, "AOT");

#if UNITY_EDITOR || !UNITY_ANDROID
            // Editor / 非 Android：直接用文件系统读取
            if (!Directory.Exists(aotDir))
            {
                Debug.LogWarning($"[HybridClrManager] AOT 元数据目录不存在: {aotDir}（Editor 下可忽略）");
                return;
            }

            var dllFiles = Directory.GetFiles(aotDir, "*.dll", SearchOption.TopDirectoryOnly);
            if (dllFiles.Length == 0)
            {
                Debug.LogWarning($"[HybridClrManager] AOT 目录为空: {aotDir}（Editor 下可忽略）");
                return;
            }

            var successCount = 0;
            foreach (var dllPath in dllFiles)
            {
                var dllName = Path.GetFileName(dllPath);
                var dllBytes = File.ReadAllBytes(dllPath);
                if (LoadOneAOT(dllName, dllBytes))
                {
                    successCount++;
                }
            }

            Debug.Log($"[HybridClrManager] AOT 元数据加载完成: {successCount}/{dllFiles.Length}");
#else
            // Android：StreamingAssets 在 .apk 内，需用 UnityWebRequest 读取
            LoadAOTAssembliesAndroid(aotDir);
#endif
        }

        /// <summary>
        /// Android 平台下加载 AOT 元数据。
        /// 通过打包时生成的 aot_files.txt 清单逐个下载。
        /// </summary>
        private static void LoadAOTAssembliesAndroid(string aotDir)
        {
            var manifestPath = aotDir + "/aot_files.txt";
            using var manifestReq = UnityWebRequest.Get(manifestPath);
            manifestReq.SendWebRequest();
            while (!manifestReq.isDone) { }

            if (manifestReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[HybridClrManager] 无法读取 AOT 清单: {manifestReq.error}（Editor 下可忽略）");
                return;
            }

            var dllNames = manifestReq.downloadHandler.text
                .Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

            var successCount = 0;
            foreach (var rawName in dllNames)
            {
                var dllName = rawName.Trim();
                if (string.IsNullOrEmpty(dllName)) continue;

                using var fileReq = UnityWebRequest.Get($"{aotDir}/{dllName}");
                fileReq.SendWebRequest();
                while (!fileReq.isDone) { }

                if (fileReq.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[HybridClrManager] 加载 AOT 失败: {dllName}, {fileReq.error}");
                    continue;
                }

                if (LoadOneAOT(dllName, fileReq.downloadHandler.data))
                {
                    successCount++;
                }
            }

            Debug.Log($"[HybridClrManager] AOT 元数据加载完成(Android): {successCount}/{dllNames.Length}");
        }

        /// <summary>
        /// 加载单个 AOT 元数据 DLL，返回是否成功。
        /// </summary>
        private static bool LoadOneAOT(string dllName, byte[] dllBytes)
        {
            var errorCode = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HomologousImageMode.SuperSet);
            if (errorCode == LoadImageErrorCode.OK)
            {
                Debug.Log($"[HybridClrManager] 加载 AOT 元数据成功: {dllName}");
                return true;
            }

            Debug.LogWarning($"[HybridClrManager] 加载 AOT 元数据失败: {dllName}, 错误码: {errorCode}");
            return false;
        }
    }
}
