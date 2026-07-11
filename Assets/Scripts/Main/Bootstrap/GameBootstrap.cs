using System.Collections;
using System.Threading.Tasks;
using Main.HybridCLR;
using UnityEngine;

namespace Main.Bootstrap
{
    /// <summary>
    /// 游戏入口：串联热更新完整流程。
    ///
    /// 流程：
    /// 1. 初始化 HybridCLR（加载 AOT 补充元数据）
    /// 2. 检查代码更新（对比远程/本地 manifest 版本）
    /// 3. 有更新则下载热更 DLL + MD5 校验
    /// 4. 加载热更 DLL 并执行入口方法（下载后立即加载，无需重启）
    ///
    /// 将此组件挂载到场景中的 GameObject 上即可。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("热更新配置")]
        [Tooltip("是否启用热更新检查。关闭后将直接加载内置热更代码。")]
        [SerializeField] private bool enableHotUpdate = true;

        /// <summary>
        /// 日志回调，将关键步骤输出到 Console（ConsoleToScreen 会捕获显示）。
        /// </summary>
        private static void Log(string msg)
        {
            Debug.Log($"[GameBootstrap] {msg}");
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            Log("启动热更新流程");
            StartCoroutine(RunHotUpdateFlow());
        }

        private IEnumerator RunHotUpdateFlow()
        {
            // ① 初始化 HybridCLR（加载 AOT 元数据）
            Log("① 初始化 HybridCLR");
            HybridClrManager.Initialize();
            Log("HybridCLR 初始化完成");

            // ② 检查并下载代码更新
            if (enableHotUpdate)
            {
                Log("② 检查代码更新");
                var checkTask = CodeUpdateManager.CheckForUpdates();
                yield return WaitForTask(checkTask);

                if (checkTask.IsFaulted)
                {
                    Log($"检查更新异常: {checkTask.Exception?.GetBaseException().Message}");
                }
                else if (checkTask.Result)
                {
                    Log("发现新版本，开始下载");
                    var downloadTask = CodeUpdateManager.DownloadAndApplyUpdates(OnDownloadProgress);
                    yield return WaitForTask(downloadTask);

                    if (downloadTask.IsFaulted)
                    {
                        Log($"下载失败: {downloadTask.Exception?.GetBaseException().Message}");
                    }
                    else if (downloadTask.Result)
                    {
                        Log("下载成功");
                    }
                    else
                    {
                        Log("下载失败（校验或写入错误）");
                    }
                }
                else
                {
                    Log("已是最新版本");
                }
            }
            else
            {
                Log("② 跳过热更新检查（已禁用）");
            }

            // ③ 加载并执行热更代码
            Log("③ 加载热更代码并执行入口");
            HotUpdateLoader.LoadAndRun(Log);

            Log("热更新流程结束");
        }

        /// <summary>
        /// 将 Task 转为 IEnumerator，在协程中逐帧等待完成。
        /// </summary>
        private static IEnumerator WaitForTask(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
        }

        private void OnDownloadProgress(float progress)
        {
            // 每 10% 输出一次，避免日志过多
            var percent = Mathf.RoundToInt(progress * 100f);
            if (percent % 10 == 0)
            {
                Log($"下载进度: {percent}%");
            }
        }
    }
}
