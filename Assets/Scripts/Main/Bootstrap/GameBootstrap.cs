using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Main.HybridCLR;

namespace Main.Bootstrap
{
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string initialSceneAddress = "MainMenu";
        [SerializeField] private bool enableHotUpdate = true;

        private void Start()
        {
            Application.targetFrameRate = 60;
            StartCoroutine(InitializeGame());
        }

        private IEnumerator InitializeGame()
        {
            // 显示加载界面
            ShowLoadingScreen();

            // 初始化HybridCLR
            if (enableHotUpdate)
            {
                yield return InitializeHybridClr();
                yield return CheckAndApplyCodeUpdate();
            }

            // 初始化Addressables
            yield return InitializeAddressables();

            // 检查资源更新
            if (enableHotUpdate)
            {
                yield return CheckAndDownloadResourceUpdates();
            }

            // 加载初始场景
            yield return LoadInitialScene();

            // 隐藏加载界面
            HideLoadingScreen();
        }

        private IEnumerator InitializeHybridClr()
        {
            // 初始化HybridCLR运行时环境
            var initTask = HybridClrManager.Initialize();
            while (!initTask.IsCompleted)
            {
                yield return null;
            }

            if (initTask.IsFaulted)
            {
                Debug.LogError($"HybridCLR初始化失败: {initTask.Exception}");
                // 处理初始化失败的情况
            }
            else
            {
                Debug.Log("HybridCLR初始化成功");
            }
        }

        private IEnumerator CheckAndApplyCodeUpdate()
        {
            // 检查代码更新
            var updateCheckTask = CodeUpdateManager.CheckForUpdates();
            while (!updateCheckTask.IsCompleted)
            {
                yield return null;
            }

            if (updateCheckTask.Result)
            {
                // 有更新，下载并应用
                var downloadTask =
                    CodeUpdateManager.DownloadAndApplyUpdates(UpdateLoadingProgress);
                while (!downloadTask.IsCompleted)
                {
                    yield return null;
                }

                if (downloadTask.IsFaulted)
                {
                    Debug.LogError($"代码更新失败: {downloadTask.Exception}");
                }
                else
                {
                    Debug.Log("代码更新成功，重启游戏");
                    // 重启游戏以应用更新
                    Application.Quit();
                }
            }
        }

        private IEnumerator InitializeAddressables()
        {
            var initHandle = Addressables.InitializeAsync();
            yield return initHandle;

            if (initHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("Addressables初始化失败");
                // 处理初始化失败的情况
            }
            else
            {
                Debug.Log("Addressables初始化成功");
            }
        }

        private IEnumerator CheckAndDownloadResourceUpdates()
        {
            // 检查Addressables资源更新
            var checkHandle = Addressables.CheckForCatalogUpdates(false);
            yield return checkHandle;

            if (checkHandle.Status == AsyncOperationStatus.Succeeded && checkHandle.Result != null &&
                checkHandle.Result.Count > 0)
            {
                Debug.Log($"发现 {checkHandle.Result.Count} 个Catalog更新");

                // 更新Catalog
                var updateHandle = Addressables.UpdateCatalogs(checkHandle.Result, false);
                yield return updateHandle;

                // 获取需要下载的资源大小
                var sizeHandle = Addressables.GetDownloadSizeAsync("*");
                yield return sizeHandle;

                if (sizeHandle.Status == AsyncOperationStatus.Succeeded && sizeHandle.Result > 0)
                {
                    Debug.Log($"需要下载 {sizeHandle.Result / 1024f / 1024f:F2} MB 资源");

                    // 下载更新
                    var downloadHandle = Addressables.DownloadDependenciesAsync("*", Addressables.MergeMode.Union);
                    while (!downloadHandle.IsDone)
                    {
                        UpdateLoadingProgress(downloadHandle.PercentComplete);
                        yield return null;
                    }

                    if (downloadHandle.Status == AsyncOperationStatus.Succeeded)
                    {
                        Debug.Log("资源更新下载完成");
                    }
                    else
                    {
                        Debug.LogError("资源更新下载失败");
                        // 处理下载失败的情况
                    }
                }
            }
        }

        private IEnumerator LoadInitialScene()
        {
            var loadHandle = Addressables.LoadSceneAsync(initialSceneAddress);
            yield return loadHandle;

            if (loadHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"加载初始场景失败: {initialSceneAddress}");
                // 处理加载失败的情况
            }
        }

        private void ShowLoadingScreen()
        {
            // 显示加载界面的实现
            Debug.Log("显示加载界面");
        }

        private void HideLoadingScreen()
        {
            // 隐藏加载界面的实现
            Debug.Log("隐藏加载界面");
        }

        /// <summary>
        /// 更新加载进度UI
        /// </summary>
        /// <param name="progress">进度</param>
        private void UpdateLoadingProgress(float progress)
        {
            // 更新加载进度UI的实现
            Debug.Log($"加载进度: {progress * 100:F2}%");
        }
    }
}