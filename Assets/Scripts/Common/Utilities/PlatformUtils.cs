using UnityEngine;

namespace Common.Utilities
{
    /// <summary>
    ///  平台工具类
    /// </summary>
    public class PlatformUtils
    {
        /// <summary>
        /// 获取当前平台名称
        /// </summary>
        public static string GetPlatformName()
        {
#if UNITY_EDITOR
            return "Editor";
#elif UNITY_STANDALONE_WIN
            return "Windows";
#elif UNITY_STANDALONE_OSX
            return "Mac";
#elif UNITY_ANDROID
            return "Android";
#elif UNITY_IOS
            return "iOS";
#else
            return "Unknown";
#endif
        }

        /// <summary>
        /// 判断是否为移动平台
        /// </summary>
        public static bool IsMobile()
        {
            return Application.isMobilePlatform;
        }

        /// <summary>
        /// 判断是否为Web平台
        /// </summary>
        public static bool IsWeb()
        {
            return Application.platform == RuntimePlatform.WebGLPlayer;
        }

        /// <summary>
        /// 判断是否为Android平台
        /// </summary>
        public static bool IsAndroid()
        {
            return Application.platform == RuntimePlatform.Android;
        }

        /// <summary>
        /// 判断是否为iOS平台
        /// </summary>
        public static bool IsIOS()
        {
            return Application.platform == RuntimePlatform.IPhonePlayer;
        }

        /// <summary>
        /// 判断是否为PC平台
        /// </summary>
        public static bool IsWindows()
        {
            return Application.platform == RuntimePlatform.WindowsPlayer ||
                   Application.platform == RuntimePlatform.WindowsEditor;
        }

        /// <summary>
        /// 判断是否为Mac平台
        /// </summary>
        public static bool IsMac()
        {
            return Application.platform == RuntimePlatform.OSXPlayer ||
                   Application.platform == RuntimePlatform.OSXEditor;
        }

        public static bool IsLinux()
        {
            return Application.platform == RuntimePlatform.LinuxPlayer ||
                   Application.platform == RuntimePlatform.LinuxEditor;
        }

        /// <summary>
        /// 判断是否为编辑器环境
        /// </summary>
        public static bool IsEditor()
        {
            return Application.isEditor;
        }
    }
}