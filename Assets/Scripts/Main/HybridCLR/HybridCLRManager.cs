using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HybridCLR;
using UnityEngine;
using UnityEngine.Networking;

namespace Main.HybridCLR
{
    public static class HybridClrManager
    {
        private static bool _initialized = false;

        private static readonly List<string> AOTAssemblies = new()
        {
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll",
            "UnityEngine.CoreModule.dll",
            // 添加更多AOT程序集
        };

        public static async Task Initialize()
        {
            if (_initialized)
            {
                return;
            }

            // 加载AOT元数据
            await LoadAOTAssemblies();

            // 初始化HybridCLR
            // RuntimeApi.Initialize();

            _initialized = true;
        }

        private static async Task LoadAOTAssemblies()
        {
            foreach (var assemblyName in AOTAssemblies)
            {
                var aotPath = Path.Combine(Application.streamingAssetsPath, "AOT", assemblyName);

                if (Application.platform == RuntimePlatform.Android)
                {
                    // 在Android上使用UnityWebRequest加载
                    using var request = UnityWebRequest.Get(aotPath);
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"加载AOT程序集失败: {assemblyName}, 错误: {request.error}");
                        continue;
                    }

                    var dllBytes = request.downloadHandler.data;
                    RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HomologousImageMode.Consistent);
                }
                else
                {
                    // 在其他平台上使用文件系统加载
                    if (File.Exists(aotPath))
                    {
                        var dllBytes = await File.ReadAllBytesAsync(aotPath);
                        RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HomologousImageMode.Consistent);
                    }
                    else
                    {
                        Debug.LogError($"AOT程序集文件不存在: {aotPath}");
                    }
                }
            }
        }
    }
}