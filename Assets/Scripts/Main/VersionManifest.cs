using System;
using System.Collections.Generic;

namespace Main
{
    /// <summary>
    /// 热更新版本清单，与 Editor 打包生成的 manifest.json 对应。
    /// 通过 JsonUtility 序列化/反序列化。
    /// </summary>
    [Serializable]
    public class VersionManifest
    {
        /// <summary>
        /// 版本号（时间戳字符串，如 "20260711143000"）。
        /// 每次打包自动生成，用于整体版本对比。
        /// </summary>
        public string version;

        /// <summary>
        /// 本次热更包含的所有 DLL 条目。
        /// </summary>
        public List<DllEntry> hotUpdateDlls = new();

        /// <summary>
        /// 根据 DLL 名称查找条目。
        /// </summary>
        public DllEntry FindDll(string name)
        {
            return hotUpdateDlls.Find(d => d.name == name);
        }
    }

    /// <summary>
    /// 单个热更 DLL 的元信息。
    /// </summary>
    [Serializable]
    public class DllEntry
    {
        /// <summary>文件名（含扩展名），如 "HotUpdate.dll"</summary>
        public string name;

        /// <summary>文件 MD5，用于校验下载完整性</summary>
        public string md5;

        /// <summary>文件字节数</summary>
        public long size;
    }
}
