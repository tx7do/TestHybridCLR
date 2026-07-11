using System.Collections;
using System.Threading.Tasks;
using Main.HybridCLR;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Main.Bootstrap
{
    /// <summary>
    /// 游戏入口：串联代码热更新 + 资源热更新的完整流程。
    ///
    /// 流程：
    /// 1. 初始化 HybridCLR（加载 AOT 补充元数据）
    /// 2. 检查并下载代码更新（DLL + MD5 校验）
    /// 3. 加载并执行热更代码入口
    /// 4. 初始化 Addressables
    /// 5. 检查 Catalog 更新 → 下载资源更新
    /// 6. 加载游戏场景（MainMenu，远程 Addressable 场景）
    ///
    /// 将此组件挂载到 Bootstrap 场景（SampleScene）中的 GameObject 上。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("代码热更新")]
        [Tooltip("是否启用代码热更新检查。")]
        [SerializeField] private bool enableHotUpdate = true;

        [Header("资源热更新")]
        [Tooltip("初始游戏场景的 Addressable 地址。")]
        [SerializeField] private string initialSceneAddress = "MainMenu";

        [Tooltip("是否启用资源热更新检查。")]
        [SerializeField] private bool enableResourceUpdate = true;

        private static void Log(string msg)
        {
            Debug.Log($"[GameBootstrap] {msg}");
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            Log("启动热更新流程");
            StartCoroutine(RunGameFlow());
        }

        private IEnumerator RunGameFlow()
        {
            // ── 代码热更新 ──────────────────────────────────

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
                    Log("发现新版本，开始下载代码");
                    var downloadTask = CodeUpdateManager.DownloadAndApplyUpdates(OnDownloadProgress);
                    yield return WaitForTask(downloadTask);

                    if (downloadTask.IsFaulted)
                    {
                        Log($"代码下载失败: {downloadTask.Exception?.GetBaseException().Message}");
                    }
                    else if (downloadTask.Result)
                    {
                        Log("代码下载成功");
                    }
                    else
                    {
                        Log("代码下载失败（校验或写入错误）");
                    }
                }
                else
                {
                    Log("代码已是最新版本");
                }
            }

            // ③ 加载并执行热更代码入口
            Log("③ 加载热更代码并执行入口");
            HotUpdateLoader.LoadAndRun(Log);

            // ── 资源热更新 ──────────────────────────────────

            // ④ 初始化 Addressables
            Log("④ 初始化 Addressables");
            var initHandle = Addressables.InitializeAsync();
            yield return initHandle;

            if (initHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Log("Addressables 初始化失败，跳过资源更新");
            }
            else
            {
                Log("Addressables 初始化成功");

                // ⑤ 检查并下载资源更新
                if (enableResourceUpdate)
                {
                    Log("⑤ 检查资源更新");
                    yield return CheckAndDownloadResourceUpdates();
                }
            }

            // ⑥ 加载游戏场景
            Log($"⑥ 加载游戏场景: {initialSceneAddress}");
            yield return LoadInitialScene();

            Log("热更新流程结束");
        }

        /// <summary>
        /// 检查 Addressables Catalog 更新，并下载所需资源。
        /// </summary>
        private IEnumerator CheckAndDownloadResourceUpdates()
        {
            // 检查 Catalog 是否有更新
            var checkHandle = Addressables.CheckForCatalogUpdates(false);
            yield return checkHandle;

            if (checkHandle.Status == AsyncOperationStatus.Succeeded
                && checkHandle.Result != null
                && checkHandle.Result.Count > 0)
            {
                Log($"发现 {checkHandle.Result.Count} 个 Catalog 更新");

                // 更新 Catalog
                var updateHandle = Addressables.UpdateCatalogs(checkHandle.Result, false);
                yield return updateHandle;
                Addressables.Release(updateHandle);

                Log("Catalog 更新完成");
            }
            else
            {
                Log("Catalog 无更新");
            }

            Addressables.Release(checkHandle);

            // 检查需要下载的资源大小
            var sizeHandle = Addressables.GetDownloadSizeAsync("*");
            yield return sizeHandle;

            if (sizeHandle.Status == AsyncOperationStatus.Succeeded && sizeHandle.Result > 0)
            {
                var sizeMB = sizeHandle.Result / 1024f / 1024f;
                Log($"需要下载 {sizeMB:F2} MB 资源");

                // 下载所有依赖资源
                var downloadHandle = Addressables.DownloadDependenciesAsync("*", Addressables.MergeMode.Union);
                while (!downloadHandle.IsDone)
                {
                    OnDownloadProgress(downloadHandle.PercentComplete);
                    yield return null;
                }

                if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    Log("资源下载完成");
                }
                else
                {
                    Log($"资源下载失败: {downloadHandle.OperationException?.Message}");
                }

                Addressables.Release(downloadHandle);
            }
            else
            {
                Log("无需下载资源（已是最新）");
            }

            Addressables.Release(sizeHandle);
        }

        /// <summary>
        /// 通过 Addressables 加载初始游戏场景。
        /// 加载完成后 Bootstrap 场景会被替换。
        /// </summary>
        private IEnumerator LoadInitialScene()
        {
            var loadHandle = Addressables.LoadSceneAsync(initialSceneAddress);

            while (!loadHandle.IsDone)
            {
                OnDownloadProgress(loadHandle.PercentComplete);
                yield return null;
            }

            if (loadHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Log($"加载场景失败: {initialSceneAddress}");
                Log($"  错误: {loadHandle.OperationException?.Message}");
                Log("  请确认已执行 Addressables 打包，且场景地址为 'MainMenu'");
                yield break;
            }

            Log($"场景加载完成: {initialSceneAddress}");
            // 注意：不释放场景 handle，场景需要保持活跃
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
            var percent = Mathf.RoundToInt(progress * 100f);
            if (percent % 10 == 0)
            {
                Log($"下载进度: {percent}%");
            }
        }
    }
}
