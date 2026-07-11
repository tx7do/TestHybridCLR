using UnityEngine;

namespace HotUpdate
{
    /// <summary>
    /// 示例热更组件：挂在 DemoCube 预制体上。
    /// 由热更程序集提供，随 Addressables 资源一起远程加载，
    /// 验证「代码热更 + 资源热更」协同工作。
    /// </summary>
    public class DemoCubeController : MonoBehaviour
    {
        [Header("旋转参数")]
        [Tooltip("每秒旋转角度")]
        public float rotateSpeed = 60f;

        [Tooltip("上下浮动幅度")]
        public float floatAmplitude = 0.5f;

        [Tooltip("上下浮动速度")]
        public float floatSpeed = 2f;

        private Vector3 _startPos;

        private void Start()
        {
            _startPos = transform.position;
            Debug.Log($"[DemoCube] 热更组件启动 @ {name}，旋转速度={rotateSpeed}°/s");

            // 修改材质颜色，让远程热更的效果可见
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                // 用一个醒目的颜色标识「这是热更加载的实例」
                renderer.material.color = new Color(1f, 0.4f, 0f); // 橙色
            }
        }

        private void Update()
        {
            // 旋转
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);

            // 上下浮动
            var yOffset = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            transform.position = _startPos + new Vector3(0f, yOffset, 0f);
        }
    }
}
