using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Main
{
    /// <summary>
    /// 热更程序集加载与入口调用。
    /// 负责从本地（已下载）或 StreamingAssets 加载热更 DLL，
    /// 并通过反射调用约定入口 <c>HotUpdate.Hello.Run()</c>。
    /// </summary>
    public static class HotUpdateLoader
    {
        /// <summary>
        /// 热更程序集名（需与 HotUpdate.asmdef 的 name 一致）。
        /// </summary>
        private const string HotUpdateAssemblyName = "HotUpdate";

        /// <summary>
        /// 约定的热更入口：类全名 + 静态方法名。
        /// </summary>
        private const string EntryClassName = "HotUpdate.Hello";
        private const string EntryMethodName = "Run";

        /// <summary>
        /// 加载热更 DLL 并执行入口方法。
        ///
        /// 优先级：
        /// 1. persistentDataPath 下已下载的热更 DLL（真机热更产物）
        /// 2. StreamingAssets 下的打包时内置 DLL（首次安装/无更新时使用）
        /// 3. Editor 下直接从已加载程序集中查找（无需加载字节）
        /// </summary>
        /// <param name="onLog">日志回调，null 则用 Debug.Log</param>
        /// <returns>true 表示成功加载并执行入口</returns>
        public static bool LoadAndRun(Action<string> onLog = null)
        {
            var log = onLog ?? Debug.Log;

            var assembly = LoadAssembly(log);
            if (assembly == null)
            {
                log("[HotUpdateLoader] 加载热更程序集失败");
                return false;
            }

            log($"[HotUpdateLoader] 成功加载程序集: {assembly.GetName().Name}");

            if (!InvokeEntry(assembly, log))
            {
                return false;
            }

            log("[HotUpdateLoader] 热更入口执行完成");
            return true;
        }

        /// <summary>
        /// 加载热更程序集。Editor 下直接返回已加载程序集。
        /// </summary>
        private static Assembly LoadAssembly(Action<string> log)
        {
#if UNITY_EDITOR
            // Editor 下 HotUpdate 程序集已被 Unity 自动加载，直接查找即可。
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == HotUpdateAssemblyName);
            if (assembly != null)
            {
                log("[HotUpdateLoader] Editor 环境，直接使用已加载程序集");
                return assembly;
            }

            log("[HotUpdateLoader] Editor 环境下未找到 HotUpdate 程序集，请确认编译无误");
            return null;
#else
            // 真机：优先从已下载目录加载（热更产物），否则回退到 StreamingAssets（内置）
            var dllBytes = ReadDownloadedDll(log) ?? ReadStreamingDll(log);
            if (dllBytes == null)
            {
                return null;
            }

            return Assembly.Load(dllBytes);
#endif
        }

        /// <summary>
        /// 读取已下载到 persistentDataPath 的热更 DLL。
        /// </summary>
        private static byte[] ReadDownloadedDll(Action<string> log)
        {
            var path = GetDownloadedDllPath();
            if (File.Exists(path))
            {
                log($"[HotUpdateLoader] 从已下载目录加载: {path}");
                return File.ReadAllBytes(path);
            }

            log("[HotUpdateLoader] 无已下载的热更 DLL，将尝试内置版本");
            return null;
        }

        /// <summary>
        /// 读取 StreamingAssets 下的热更 DLL（首次安装时的内置版本）。
        /// 通过 UnityWebRequest 读取以兼容 Android 平台。
        /// </summary>
        private static byte[] ReadStreamingDll(Action<string> log)
        {
            var path = GetStreamingDllPath();

#if UNITY_ANDROID && !UNITY_EDITOR
            return ReadStreamingFromAndroid(path, log);
#else
            if (File.Exists(path))
            {
                log($"[HotUpdateLoader] 从 StreamingAssets 加载: {path}");
                return File.ReadAllBytes(path);
            }

            log($"[HotUpdateLoader] StreamingAssets 中不存在热更 DLL: {path}");
            return null;
#endif
        }

        /// <summary>
        /// Android 平台下用 UnityWebRequest 读取 StreamingAssets。
        /// </summary>
        private static byte[] ReadStreamingFromAndroid(string path, Action<string> log)
        {
            // UnityWebRequest 不能在非主线程使用，此处由调用方在协程上下文中保证主线程。
            using var request = UnityEngine.Networking.UnityWebRequest.Get(path);
            var op = request.SendWebRequest();
            while (!op.isDone)
            {
                // 在主线程同步等待
            }

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                log($"[HotUpdateLoader] 读取 StreamingAssets 失败: {request.error}");
                return null;
            }

            log($"[HotUpdateLoader] 从 StreamingAssets(Android) 加载: {path}");
            return request.downloadHandler.data;
        }

        /// <summary>
        /// 反射调用热更入口方法。
        /// </summary>
        private static bool InvokeEntry(Assembly assembly, Action<string> log)
        {
            var entryType = assembly.GetType(EntryClassName);
            if (entryType == null)
            {
                log($"[HotUpdateLoader] 未找到入口类: {EntryClassName}");
                return false;
            }

            var method = entryType.GetMethod(EntryMethodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                log($"[HotUpdateLoader] 未找到入口方法: {EntryClassName}.{EntryMethodName}");
                return false;
            }

            try
            {
                method.Invoke(null, null);
                return true;
            }
            catch (Exception e)
            {
                log($"[HotUpdateLoader] 入口方法执行异常: {e}");
                return false;
            }
        }

        // ── 路径约定 ──────────────────────────────────────────────

        /// <summary>已下载的热更 DLL 存储路径（persistentDataPath/Data/HotUpdate.dll）</summary>
        public static string GetDownloadedDllPath()
        {
            return Path.Combine(Application.persistentDataPath, "Data", "HotUpdate.dll");
        }

        /// <summary>StreamingAssets 下热更 DLL 路径</summary>
        public static string GetStreamingDllPath()
        {
            return Path.Combine(Application.streamingAssetsPath, "HotUpdate.dll");
        }

        /// <summary>已下载的版本清单路径</summary>
        public static string GetDownloadedManifestPath()
        {
            return Path.Combine(Application.persistentDataPath, "Data", "manifest.json");
        }
    }
}
