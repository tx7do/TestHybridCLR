using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace Main.HybridCLR
{
    public static class CodeUpdateManager
    {
        private const string RemoteVersionUrl = "https://your-server.com/code/version.txt";
        private const string RemoteDllUrl = "https://your-server.com/code/HotUpdateScripts.dll";
        private const string LocalDllPath = "/Data/HotUpdateScripts.dll";
        private const string LocalVersionPath = "/Data/version.txt";

        public static async Task<bool> CheckForUpdates()
        {
            try
            {
                // 获取本地版本
                var localVersion = GetLocalVersion();

                // 获取远程版本
                using var client = new HttpClient();
                var remoteVersion = await client.GetStringAsync(RemoteVersionUrl);
                return !string.Equals(localVersion, remoteVersion, StringComparison.Ordinal);
            }
            catch (Exception e)
            {
                Debug.LogError($"检查代码更新失败: {e}");
                return false;
            }
        }

        public static async Task DownloadAndApplyUpdates(Action<float> onProgress = null)
        {
            try
            {
                var directory = Path.GetDirectoryName(Application.persistentDataPath + LocalDllPath);
                if (directory == null)
                {
                    Debug.LogError("无法获取本地存储目录");
                    return;
                }

                // 创建本地存储目录
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 下载新DLL
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(RemoteDllUrl);
                    response.EnsureSuccessStatusCode();
                    var total = response.Content.Headers.ContentLength ?? -1L;
                    var canReport = total != -1 && onProgress != null;

                    await using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        var dllBytes = new byte[total];
                        var bytesRead = 0;
                        int read;

                        while ((read = await stream.ReadAsync(dllBytes, bytesRead, dllBytes.Length - bytesRead)) > 0)
                        {
                            bytesRead += read;
                            if (canReport)
                            {
                                onProgress?.Invoke((float)bytesRead / total);
                            }
                        }

                        if (bytesRead != total)
                        {
                            Debug.LogError("下载的DLL文件大小不正确");
                            return;
                        }

                        await File.WriteAllBytesAsync(Application.persistentDataPath + LocalDllPath, dllBytes);
                    }

                    // 保存新版本号
                    var remoteVersion = await client.GetStringAsync(RemoteVersionUrl);
                    await File.WriteAllTextAsync(Application.persistentDataPath + LocalVersionPath, remoteVersion);
                }

                // 加载更新后的DLL
                LoadUpdatedAssembly();
            }
            catch (Exception e)
            {
                Debug.LogError($"下载和应用代码更新失败: {e}");
                throw;
            }
        }

        private static string GetLocalVersion()
        {
            var versionPath = Application.persistentDataPath + LocalVersionPath;
            return File.Exists(versionPath) ? File.ReadAllText(versionPath) : string.Empty;
        }

        private static void LoadUpdatedAssembly()
        {
            try
            {
                var dllPath = Application.persistentDataPath + LocalDllPath;
                if (File.Exists(dllPath))
                {
                    var dllBytes = File.ReadAllBytes(dllPath);
                    var assembly = System.Reflection.Assembly.Load(dllBytes);
                    Debug.Log($"成功加载更新后的程序集: {assembly.FullName}");
                }
                else
                {
                    Debug.LogError("更新后的DLL文件不存在");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"加载更新后的程序集失败: {e}");
            }
        }
    }
}