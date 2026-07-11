using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Common.Utilities;
using UnityEngine;

namespace Main.HybridCLR
{
    /// <summary>
    /// 代码热更新管理器：检查版本 → 下载 DLL → MD5 校验 → 保存到本地。
    ///
    /// 版本对比基于远程 manifest.json 与本地已保存的 manifest.json 的 version 字段。
    /// 下载完成后通过 MD5 校验文件完整性。
    ///
    /// 注意：实际的 Assembly.Load 与入口执行由 <see cref="HotUpdateLoader"/> 负责，
    /// 本类只负责"取数据 + 校验"，职责单一。
    /// </summary>
    public static class CodeUpdateManager
    {
        /// <summary>
        /// 热更服务器根地址。
        /// 本地测试默认 http://localhost:8080，上线时改为正式 CDN 地址即可。
        /// </summary>
        private const string ServerBaseUrl = "http://localhost:8080";

        /// <summary>远程版本清单 URL</summary>
        private static string RemoteManifestUrl => $"{ServerBaseUrl}/manifest.json";

        /// <summary>远程热更 DLL URL（文件名与程序集名一致）</summary>
        private static string RemoteDllUrl => $"{ServerBaseUrl}/HotUpdate.dll";

        /// <summary>本地数据目录（persistentDataPath/Data/）</summary>
        private static string LocalDataDir => Path.Combine(Application.persistentDataPath, "Data");

        /// <summary>本地已下载的 DLL 路径</summary>
        private static string LocalDllPath => Path.Combine(LocalDataDir, "HotUpdate.dll");

        /// <summary>本地已保存的 manifest 路径</summary>
        private static string LocalManifestPath => Path.Combine(LocalDataDir, "manifest.json");

        // 共享 HttpClient，避免每次请求创建新实例
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// 获取远程版本清单。
        /// </summary>
        /// <returns>远程清单；获取失败返回 null</returns>
        public static async Task<VersionManifest> FetchRemoteManifest()
        {
            try
            {
                var json = await HttpClient.GetStringAsync(RemoteManifestUrl);
                var manifest = JsonUtility.FromJson<VersionManifest>(json);
                if (manifest == null || string.IsNullOrEmpty(manifest.version))
                {
                    Debug.LogError("[CodeUpdateManager] 远程 manifest 格式无效");
                    return null;
                }

                return manifest;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CodeUpdateManager] 获取远程 manifest 失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 读取本地已保存的版本清单。
        /// </summary>
        public static VersionManifest GetLocalManifest()
        {
            if (!File.Exists(LocalManifestPath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(LocalManifestPath);
                return JsonUtility.FromJson<VersionManifest>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CodeUpdateManager] 读取本地 manifest 失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 检查是否有更新：对比远程与本地 manifest 的 version 字段。
        /// </summary>
        /// <returns>true 表示有更新</returns>
        public static async Task<bool> CheckForUpdates()
        {
            var remote = await FetchRemoteManifest();
            if (remote == null)
            {
                return false;
            }

            var local = GetLocalManifest();
            if (local == null)
            {
                Debug.Log("[CodeUpdateManager] 本地无版本记录，需要下载");
                return true;
            }

            var hasUpdate = !string.Equals(local.version, remote.version, StringComparison.Ordinal);
            Debug.Log($"[CodeUpdateManager] 版本对比: 本地={local.version}, 远程={remote.version}, 有更新={hasUpdate}");
            return hasUpdate;
        }

        /// <summary>
        /// 下载热更 DLL 并校验 MD5。
        /// </summary>
        /// <param name="onProgress">下载进度回调 (0~1)，可为 null</param>
        /// <returns>true 表示下载并校验成功</returns>
        public static async Task<bool> DownloadAndApplyUpdates(Action<float> onProgress = null)
        {
            var remote = await FetchRemoteManifest();
            if (remote == null)
            {
                return false;
            }

            EnsureLocalDir();

            try
            {
                Debug.Log($"[CodeUpdateManager] 开始下载: {RemoteDllUrl}");
                var dllBytes = await DownloadFile(RemoteDllUrl, onProgress);

                // MD5 校验
                var md5 = ComputeMD5(dllBytes);
                var entry = remote.FindDll("HotUpdate.dll");
                if (entry != null && !string.IsNullOrEmpty(entry.md5))
                {
                    if (!string.Equals(md5, entry.md5, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogError($"[CodeUpdateManager] MD5 校验失败: 计算={md5}, 预期={entry.md5}");
                        return false;
                    }

                    Debug.Log($"[CodeUpdateManager] MD5 校验通过: {md5}");
                }

                // 写入本地文件
                await File.WriteAllBytesAsync(LocalDllPath, dllBytes);

                // 保存 manifest（记录当前版本）
                var manifestJson = JsonUtility.ToJson(remote, true);
                await File.WriteAllTextAsync(LocalManifestPath, manifestJson);

                Debug.Log($"[CodeUpdateManager] 下载完成并已保存，版本: {remote.version}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CodeUpdateManager] 下载失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 下载文件，返回完整字节数组。
        /// 使用 MemoryStream 以支持 ContentLength 未知的情况。
        /// </summary>
        private static async Task<byte[]> DownloadFile(string url, Action<float> onProgress)
        {
            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            using var stream = await response.Content.ReadAsStreamAsync();
            using var memory = new MemoryStream();

            var buffer = new byte[81920];
            int read;
            long downloaded = 0;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                memory.Write(buffer, 0, read);
                downloaded += read;
                if (total > 0)
                {
                    onProgress?.Invoke((float)downloaded / total);
                }
            }

            onProgress?.Invoke(1f);
            return memory.ToArray();
        }

        // ── 辅助方法 ──────────────────────────────────────────────

        private static void EnsureLocalDir()
        {
            if (!Directory.Exists(LocalDataDir))
            {
                Directory.CreateDirectory(LocalDataDir);
            }
        }

        private static string ComputeMD5(byte[] data)
        {
            // 复用项目已有的 CryptoUtils 计算 MD5。
            // CryptoUtils.MD5File 接收文件路径，这里先写出临时文件再计算，
            // 避免与内联 MD5 实现重复，保持单一数据源。
            var tempPath = LocalDllPath + ".tmp_md5";
            File.WriteAllBytes(tempPath, data);
            var md5 = CryptoUtils.MD5File(tempPath);
            File.Delete(tempPath);
            return md5;
        }
    }
}
