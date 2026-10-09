using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 棋盘选中高亮显示。
    /// 监听 BoardInputController 的选中状态，显示高亮效果。
    /// </summary>
    [RequireComponent(typeof(BoardInputController))]
    [RequireComponent(typeof(BoardWorldMapper))]
    public class BoardSelectionHighlight : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private BoardInputController inputController;
        [SerializeField] private BoardWorldMapper mapper;

        [Header("高亮设置")]
        [SerializeField] private GameObject highlightPrefab;
        [SerializeField] private Color selectedColor = new Color(1f, 1f, 0f, 0.5f);
        [SerializeField] private Color availableMoveColor = new Color(0f, 1f, 0f, 0.3f);
        [SerializeField] private float highlightHeight = 0.1f;

        private GameObject selectedHighlight;
        private GameObject[] moveHighlights = new GameObject[4];
        private Material highlightMaterial;

        private void Awake()
        {
            if (inputController == null) inputController = GetComponent<BoardInputController>();
            if (mapper == null) mapper = GetComponent<BoardWorldMapper>();
        }

        private void Start()
        {
            InitializeHighlights();
        }

        private void Update()
        {
            UpdateHighlights();
        }

        private void OnDestroy()
        {
            CleanupHighlights();
        }

        private void InitializeHighlights()
        {
            if (highlightPrefab != null)
            {
                selectedHighlight = Instantiate(highlightPrefab, transform);
                selectedHighlight.SetActive(false);

                for (int i = 0; i < moveHighlights.Length; i++)
                {
                    moveHighlights[i] = Instantiate(highlightPrefab, transform);
                    moveHighlights[i].SetActive(false);
                }
            }
            else
            {
                // 创建默认高亮（简单的Quad）
                selectedHighlight = CreateDefaultHighlight(selectedColor);
                for (int i = 0; i < moveHighlights.Length; i++)
                {
                    moveHighlights[i] = CreateDefaultHighlight(availableMoveColor);
                }
            }
        }

        private GameObject CreateDefaultHighlight(Color color)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.SetParent(transform);
            quad.transform.rotation = Quaternion.Euler(90, 0, 0);
            quad.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

            if (highlightMaterial == null)
            {
                highlightMaterial = new Material(Shader.Find("Standard"));
                highlightMaterial.SetFloat("_Mode", 3); // Transparent
                highlightMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                highlightMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                highlightMaterial.SetInt("_ZWrite", 0);
                highlightMaterial.DisableKeyword("_ALPHATEST_ON");
                highlightMaterial.EnableKeyword("_ALPHABLEND_ON");
                highlightMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                highlightMaterial.renderQueue = 3000;
            }

            Material mat = new Material(highlightMaterial);
            mat.color = color;
            quad.GetComponent<Renderer>().material = mat;

            Destroy(quad.GetComponent<Collider>());
            quad.SetActive(false);
            return quad;
        }

        private void UpdateHighlights()
        {
            if (inputController == null || mapper == null) return;

            if (!inputController.HasSelection)
            {
                HideAllHighlights();
                return;
            }

            // 显示选中高亮
            GridCoord selected = inputController.SelectedCoord;
            Vector3 worldPos = mapper.GridToWorld(selected);
            worldPos.y = highlightHeight;
            selectedHighlight.transform.position = worldPos;
            selectedHighlight.SetActive(true);

            // 显示可移动位置高亮
            var controller = GetComponent<BoardController>();
            if (controller != null && controller.Board != null)
            {
                var moves = controller.Board.GetAvailableMoves(selected);
                for (int i = 0; i < moveHighlights.Length; i++)
                {
                    if (i < moves.Count)
                    {
                        Vector3 movePos = mapper.GridToWorld(moves[i]);
                        movePos.y = highlightHeight;
                        moveHighlights[i].transform.position = movePos;
                        moveHighlights[i].SetActive(true);
                    }
                    else
                    {
                        moveHighlights[i].SetActive(false);
                    }
                }
            }
        }

        private void HideAllHighlights()
        {
            if (selectedHighlight != null)
                selectedHighlight.SetActive(false);

            foreach (var highlight in moveHighlights)
            {
                if (highlight != null)
                    highlight.SetActive(false);
            }
        }

        private void CleanupHighlights()
        {
            if (selectedHighlight != null) Destroy(selectedHighlight);
            foreach (var highlight in moveHighlights)
            {
                if (highlight != null) Destroy(highlight);
            }
        }
    }
}
