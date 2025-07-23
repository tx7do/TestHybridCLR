using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace Main.HybridCLR
{
    public class HotUpdateManager : MonoBehaviour
    {
        [Header("热更新配置")] [SerializeField] private string remoteDllPath = "https://your-server.com/hotfix/";
        [SerializeField] private List<string> dllNames = new() { "HotUpdate.dll" };

        private IEnumerator Start()
        {
            yield return StartCoroutine(LoadHotUpdateDlls());

            // 热更新完成后，初始化游戏
            // GameManager gameManager = gameObject.AddComponent<GameManager>();
        }

        private IEnumerator LoadHotUpdateDlls()
        {
            Debug.Log("开始检查热更新DLL...");

            foreach (var dllName in dllNames)
            {
                var localPath = Path.Combine(Application.persistentDataPath, dllName);
                var remotePath = remoteDllPath + dllName;

                // 检查本地是否有DLL
                if (!File.Exists(localPath))
                {
                    Debug.Log($"下载DLL: {dllName}");
                    yield return StartCoroutine(DownloadDll(remotePath, localPath));
                }
                else
                {
                    // 这里可以添加版本比较逻辑
                    Debug.Log($"使用本地DLL: {dllName}");
                }

                // 加载DLL
                LoadDll(localPath);
            }

            Debug.Log("热更新DLL加载完成");
        }

        private static IEnumerator DownloadDll(string url, string savePath)
        {
            using var www = UnityWebRequest.Get(url);
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(savePath, www.downloadHandler.data);
                Debug.Log($"DLL下载成功: {savePath}");
            }
            else
            {
                Debug.LogError($"DLL下载失败: {www.error}");
            }
        }

        private static Assembly LoadDll(string path)
        {
            var dllBytes = File.ReadAllBytes(path);
            if (dllBytes == null || dllBytes.Length == 0)
            {
                Debug.LogError($"无法加载DLL，文件为空或不存在: {path}");
                return null;
            }

            var assembly = Assembly.Load(dllBytes);
            if (assembly == null)
            {
                Debug.LogError($"加载DLL失败: {path}");
                return null;
            }

            Debug.Log($"加载DLL: {assembly.FullName}");

            return assembly;
        }
        
        public static Version GetDllVersion(string dllPath)
        {
            var assemblyName = AssemblyName.GetAssemblyName(dllPath);
            return assemblyName.Version;
        }
    }
}