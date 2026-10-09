using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 根据棋子属性自动设置视觉效果的辅助组件。
    /// 挂到棋子预制体上，BoardView创建实例时会自动调用。
    /// </summary>
    [DisallowMultipleComponent]
    public class PieceVisualizer : MonoBehaviour
    {
        [Header("材质配置")]
        [SerializeField] private Material fireMaterial;
        [SerializeField] private Material waterMaterial;
        [SerializeField] private Material windMaterial;
        [SerializeField] private Material groundMaterial;
        [SerializeField] private Material defaultMaterial;

        [Header("颜色配置（没有材质时使用）")]
        [SerializeField] private Color fireColor = new Color(1f, 0.3f, 0.1f);      // 红色
        [SerializeField] private Color waterColor = new Color(0.1f, 0.5f, 1f);     // 蓝色
        [SerializeField] private Color windColor = new Color(0.9f, 0.9f, 0.9f);    // 白色
        [SerializeField] private Color groundColor = new Color(0.8f, 0.6f, 0.2f);  // 棕黄色

        private Renderer meshRenderer;

        private void Awake()
        {
            meshRenderer = GetComponent<Renderer>();
            if (meshRenderer == null)
                meshRenderer = GetComponentInChildren<Renderer>();
        }

        /// <summary>
        /// 根据元素类型设置视觉效果。由外部调用。
        /// </summary>
        public void SetElement(ElementType element)
        {
            if (meshRenderer == null) return;

            Material mat = GetMaterialForElement(element);
            if (mat != null)
            {
                meshRenderer.material = mat;
            }
            else
            {
                Color color = GetColorForElement(element);
                if (meshRenderer.material == null)
                    meshRenderer.material = new Material(Shader.Find("Standard"));
                meshRenderer.material.color = color;
            }
        }

        private Material GetMaterialForElement(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return fireMaterial;
                case ElementType.Water: return waterMaterial;
                case ElementType.Wind: return windMaterial;
                case ElementType.ground: return groundMaterial;
                default: return defaultMaterial;
            }
        }

        private Color GetColorForElement(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return fireColor;
                case ElementType.Water: return waterColor;
                case ElementType.Wind: return windColor;
                case ElementType.ground: return groundColor;
                default: return Color.white;
            }
        }
    }
}
