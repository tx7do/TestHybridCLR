using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Main
{
    /// <summary>
    /// 挂在 MainMenu 场景上，通过 Addressables 实例化 DemoCube 预制体。
    ///
    /// 这个组件本身是非热更代码（Assembly-CSharp），但它实例化的
    /// DemoCube 预制体上挂着热更程序集的 DemoCubeController 组件，
    /// 从而验证「资源热更 + 代码热更」协同工作。
    /// </summary>
    public class DemoSceneLoader : MonoBehaviour
    {
        /// <summary>DemoCube 的 Addressable 地址</summary>
        private const string DemoCubeAddress = "DemoCube";

        private static void Log(string msg)
        {
            Debug.Log($"[DemoSceneLoader] {msg}");
        }

        private IEnumerator Start()
        {
            Log($"MainMenu 场景启动，加载 DemoCube: {DemoCubeAddress}");

            var handle = Addressables.InstantiateAsync(DemoCubeAddress);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var instance = handle.Result;
                // 放到摄像机前方可见位置
                instance.transform.position = new Vector3(0, 0, 5);
                Log("DemoCube 实例化成功（热更代码+资源协同验证）");
            }
            else
            {
                Log($"DemoCube 加载失败: {handle.OperationException?.Message}");
            }
        }
    }
}
