using System;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 负责逻辑坐标和 Unity 世界坐标之间的转换。
    /// localOrigin 是逻辑坐标 (0,0) 的格子中心；localStepX/Y 是相邻格子中心之间的位移。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardWorldMapper : MonoBehaviour
    {
        [SerializeField] private Transform boardRoot;
        [Tooltip("逻辑坐标 (0,0) 格子中心，相对于 Board Root 的局部坐标。")]
        [SerializeField] private Vector3 localOrigin = Vector3.zero;
        [Tooltip("逻辑 X 增加 1 时，世界位置的局部位移。")]
        [SerializeField] private Vector3 localStepX = Vector3.right;
        [Tooltip("逻辑 Y 增加 1 时，世界位置的局部位移。")]
        [SerializeField] private Vector3 localStepY = Vector3.forward;
        [Tooltip("点击位置距离格子中心超过此距离时，不认定为点击该格子。单位为 Board Root 的局部单位。")]
        [SerializeField, Min(0.01f)] private float maxSnapDistance = 0.45f;
        [Header("编辑器辅助线")]
        [SerializeField, Min(0)] private int previewWidth = 10;
        [SerializeField, Min(0)] private int previewHeight = 10;
        [SerializeField] private bool drawGizmos = true;

        public Transform Root { get { return boardRoot == null ? transform : boardRoot; } }
        public Vector3 LocalOrigin { get { return localOrigin; } }
        public Vector3 LocalStepX { get { return localStepX; } }
        public Vector3 LocalStepY { get { return localStepY; } }

        public Vector3 GridToWorld(GridCoord coord)
        {
            Vector3 local = localOrigin + localStepX * coord.X + localStepY * coord.Y;
            return Root.TransformPoint(local);
        }

        public bool TryWorldToGrid(
            Vector3 worldPosition,
            int width,
            int height,
            out GridCoord coord)
        {
            coord = default(GridCoord);
            if (width <= 0 || height <= 0) return false;
            if (!HasValidBasis()) return false;

            Vector3 local = Root.InverseTransformPoint(worldPosition);
            Vector3 delta = local - localOrigin;
            float xx = Vector3.Dot(localStepX, localStepX);
            float xy = Vector3.Dot(localStepX, localStepY);
            float yy = Vector3.Dot(localStepY, localStepY);
            float determinant = xx * yy - xy * xy;
            if (Mathf.Abs(determinant) < 0.000001f) return false;

            float x = (Vector3.Dot(delta, localStepX) * yy - Vector3.Dot(delta, localStepY) * xy) / determinant;
            float y = (Vector3.Dot(delta, localStepY) * xx - Vector3.Dot(delta, localStepX) * xy) / determinant;
            int gridX = Mathf.RoundToInt(x);
            int gridY = Mathf.RoundToInt(y);
            if (gridX < 0 || gridX >= width || gridY < 0 || gridY >= height) return false;

            GridCoord candidate = new GridCoord(gridX, gridY);
            Vector3 nearestLocal = localOrigin + localStepX * gridX + localStepY * gridY;
            if (Vector3.Distance(local, nearestLocal) > maxSnapDistance) return false;

            coord = candidate;
            return true;
        }

        public bool TryRayToGrid(
            Ray ray,
            int width,
            int height,
            out GridCoord coord)
        {
            coord = default(GridCoord);
            if (!HasValidBasis()) return false;
            Vector3 worldOrigin = GridToWorld(new GridCoord(0, 0));
            Vector3 worldX = Root.TransformDirection(localStepX);
            Vector3 worldY = Root.TransformDirection(localStepY);
            Vector3 normal = Vector3.Cross(worldX, worldY);
            if (normal.sqrMagnitude < 0.000001f) return false;

            Plane plane = new Plane(normal.normalized, worldOrigin);
            float distance;
            if (!plane.Raycast(ray, out distance) || distance < 0f) return false;
            return TryWorldToGrid(ray.GetPoint(distance), width, height, out coord);
        }

        private bool HasValidBasis()
        {
            return localStepX.sqrMagnitude > 0.000001f &&
                   localStepY.sqrMagnitude > 0.000001f &&
                   Vector3.Cross(localStepX, localStepY).sqrMagnitude > 0.000001f;
        }

        private void OnValidate()
        {
            if (maxSnapDistance < 0.01f) maxSnapDistance = 0.01f;
            if (previewWidth < 0) previewWidth = 0;
            if (previewHeight < 0) previewHeight = 0;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || previewWidth <= 0 || previewHeight <= 0 || !HasValidBasis()) return;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.45f);
            for (int x = 0; x < previewWidth; x++)
            for (int y = 0; y < previewHeight; y++)
            {
                Vector3 center = GridToWorld(new GridCoord(x, y));
                Gizmos.DrawWireSphere(center, maxSnapDistance * 0.18f);
            }
        }
    }
}
