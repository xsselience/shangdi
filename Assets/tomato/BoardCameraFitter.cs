using UnityEngine;

[RequireComponent(typeof(Camera))]
public class BoardCameraFitter : MonoBehaviour
{
    [Header("引用")]
    public Gridmap map;

    [Header("取景参数")]
    [Range(0.2f, 1f)]
    [Tooltip("棋盘占屏幕短边的比例。1 = 贴边，0.7 = 留白明显")]
    public float screenFill = 0.8f;

    [Tooltip("相机相对棋盘中心的 z 偏移")]
    public float cameraZ = -10f;

    // 这个脚本里没有 Update、没有 OnValidate、没有 OnEnable。
    // 它只在你右键点菜单的时候动一次相机，其余时间完全不参与。
    // 觉得不需要了，直接把这个组件删掉也不影响任何东西。

    [ContextMenu("Fit 一次（对准棋盘）")]
    public void Fit()
    {
        if (map == null) { Debug.LogWarning("BoardCameraFitter: map 未指定", this); return; }

        var cam = GetComponent<Camera>();
        if (cam == null) return;

        float aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;

        float halfH = map.height * map.cellSize * 0.5f;
        float halfW = map.width * map.cellSize * 0.5f;

        float tight = Mathf.Max(halfH, halfW / aspect);
        float fill = Mathf.Clamp(screenFill, 0.05f, 1f);

        cam.orthographic = true;
        cam.orthographicSize = tight / fill;
        cam.transform.rotation = Quaternion.identity;

        Vector3 c = map.BoardCenterWorld;
        cam.transform.position = new Vector3(c.x, c.y, c.z + cameraZ);
    }
}
