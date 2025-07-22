using System;
using System.Linq;
using UnityEngine;

public class LoadDll : MonoBehaviour
{
    private void Start()
    {
        // Editor环境下，HotUpdate.dll.bytes已经被自动加载，不需要加载，重复加载反而会出问题。
#if !UNITY_EDITOR
        Assembly hotUpdateAss =
 Assembly.Load(File.ReadAllBytes($"{Application.streamingAssetsPath}/HotUpdate.dll.bytes"));
#else
        // Editor下无需加载，直接查找获得HotUpdate程序集
        var hotUpdateAss =
            AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "HotUpdate");
#endif

        var helloClass = hotUpdateAss.GetType("HotUpdate.Hello");
        if (helloClass == null)
        {
            Debug.LogError("Class 'Hello' not found in 'HotUpdate' assembly.");
            return;
        }

        var runMethod = helloClass.GetMethod("Run");
        if (runMethod == null)
        {
            Debug.LogError("Method 'Run' not found in 'Hello' class.");
            return;
        }

        runMethod.Invoke(null, null);
    }
}