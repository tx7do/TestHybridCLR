using UnityEngine;

namespace HotUpdate
{
    public class Hello
    {
        public static void Run()
        {
            // Debug.Log("Hello, HybridCLR");

            Debug.Log("Hello, World");

            var go = new GameObject("Test1");
            go.AddComponent<Print>();
        }
    }
}