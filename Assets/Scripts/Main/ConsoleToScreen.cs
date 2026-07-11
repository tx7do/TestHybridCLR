using System;
using System.Collections.Generic;
using UnityEngine;

namespace Main
{
    /// <summary>
    /// 将 Debug.Log 输出捕获并显示在屏幕上，方便无日志面板的平台（如真机）查看。
    /// 挂载到 Bootstrap 场景的 GameObject 上即可。
    /// </summary>
    public class ConsoleToScreen : MonoBehaviour
    {
        private const int MaxLines = 50;
        private const int MaxLineLength = 120;

        private readonly List<string> _lines = new();
        private string _logStr = "";

        [SerializeField] private int fontSize = 15;

        private void Log(string logString, string stackTrace, LogType type)
        {
            foreach (var line in logString.Split('\n'))
            {
                if (line.Length <= MaxLineLength)
                {
                    _lines.Add(line);
                    continue;
                }

                var lineCount = line.Length / MaxLineLength + 1;
                for (var i = 0; i < lineCount; i++)
                {
                    if ((i + 1) * MaxLineLength <= line.Length)
                    {
                        _lines.Add(line.Substring(i * MaxLineLength, MaxLineLength));
                    }
                    else
                    {
                        _lines.Add(line.Substring(i * MaxLineLength, line.Length - i * MaxLineLength));
                    }
                }
            }

            if (_lines.Count > MaxLines)
            {
                _lines.RemoveRange(0, _lines.Count - MaxLines);
            }

            _logStr = string.Join("\n", _lines);
        }

        private void OnEnable()
        {
            Application.logMessageReceived += Log;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= Log;
        }

        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(Screen.width / 1200.0f, Screen.height / 800.0f, 1.0f));
            GUI.Label(new Rect(10, 10, 800, 370), _logStr,
                new GUIStyle { fontSize = Math.Max(10, fontSize) });
        }
    }
}
