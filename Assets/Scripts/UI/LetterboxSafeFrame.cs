using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public sealed class LetterboxSafeFrame : MonoBehaviour
    {
        [SerializeField] private RectTransform safeFrame;
        [SerializeField] private RectTransform topBar;
        [SerializeField] private RectTransform bottomBar;
        [SerializeField] private RectTransform leftBar;
        [SerializeField] private RectTransform rightBar;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

        private Vector2 lastRootSize;

        public static RectTransform Install(Canvas canvas, bool moveExistingChildren, string safeFrameName)
        {
            if (canvas == null)
                return null;

            RectTransform root = canvas.GetComponent<RectTransform>();
            if (root == null)
                return null;

            LetterboxSafeFrame existing = canvas.GetComponent<LetterboxSafeFrame>();
            if (existing != null)
            {
                if (moveExistingChildren)
                    existing.MoveRootChildrenIntoSafeFrame();

                return existing.safeFrame;
            }

            Transform[] originalChildren = moveExistingChildren ? GetDirectChildren(root) : null;
            RectTransform safe = CreateRect(safeFrameName, root);
            RectTransform top = CreateBar("LetterboxTop", root);
            RectTransform bottom = CreateBar("LetterboxBottom", root);
            RectTransform left = CreateBar("LetterboxLeft", root);
            RectTransform right = CreateBar("LetterboxRight", root);

            LetterboxSafeFrame frame = canvas.gameObject.AddComponent<LetterboxSafeFrame>();
            frame.Initialize(safe, top, bottom, left, right);

            if (moveExistingChildren)
                frame.MoveChildrenIntoSafeFrame(originalChildren);

            return safe;
        }

        public void Initialize(RectTransform frame, RectTransform top, RectTransform bottom, RectTransform left, RectTransform right)
        {
            safeFrame = frame;
            topBar = top;
            bottomBar = bottom;
            leftBar = left;
            rightBar = right;
            UpdateLayout(true);
        }

        private void MoveRootChildrenIntoSafeFrame()
        {
            RectTransform root = transform as RectTransform;
            if (root == null || safeFrame == null)
                return;

            MoveChildrenIntoSafeFrame(GetDirectChildren(root));
        }

        private void MoveChildrenIntoSafeFrame(Transform[] children)
        {
            if (safeFrame == null || children == null)
                return;

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == null || child == safeFrame || child == topBar || child == bottomBar || child == leftBar || child == rightBar)
                    continue;

                child.SetParent(safeFrame, false);
                child.SetSiblingIndex(i);
            }
        }

        private void LateUpdate()
        {
            UpdateLayout(false);
        }

        private void OnRectTransformDimensionsChange()
        {
            UpdateLayout(true);
        }

        private void UpdateLayout(bool force)
        {
            RectTransform root = transform as RectTransform;
            if (root == null || safeFrame == null)
                return;

            Vector2 rootSize = root.rect.size;
            if (rootSize.x <= 0f || rootSize.y <= 0f)
                return;

            if (!force && Vector2.SqrMagnitude(rootSize - lastRootSize) < 0.01f)
                return;

            lastRootSize = rootSize;

            float scale = Mathf.Min(rootSize.x / referenceResolution.x, rootSize.y / referenceResolution.y);
            Vector2 safeSize = referenceResolution * scale;

            ApplyRect(safeFrame, Vector2.zero, referenceResolution);
            safeFrame.localScale = new Vector3(scale, scale, 1f);

            float sideWidth = Mathf.Max(0f, (rootSize.x - safeSize.x) * 0.5f);
            float verticalHeight = Mathf.Max(0f, (rootSize.y - safeSize.y) * 0.5f);

            SetBar(leftBar, sideWidth > 0.5f, new Vector2(-(safeSize.x * 0.5f + sideWidth * 0.5f), 0f), new Vector2(sideWidth, rootSize.y));
            SetBar(rightBar, sideWidth > 0.5f, new Vector2(safeSize.x * 0.5f + sideWidth * 0.5f, 0f), new Vector2(sideWidth, rootSize.y));
            SetBar(topBar, verticalHeight > 0.5f, new Vector2(0f, safeSize.y * 0.5f + verticalHeight * 0.5f), new Vector2(rootSize.x, verticalHeight));
            SetBar(bottomBar, verticalHeight > 0.5f, new Vector2(0f, -(safeSize.y * 0.5f + verticalHeight * 0.5f)), new Vector2(rootSize.x, verticalHeight));
        }

        private static void SetBar(RectTransform rect, bool active, Vector2 position, Vector2 size)
        {
            if (rect == null)
                return;

            rect.gameObject.SetActive(active);
            if (active)
                ApplyRect(rect, position, size);
        }

        private static void ApplyRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Transform[] GetDirectChildren(Transform root)
        {
            Transform[] children = new Transform[root.childCount];
            for (int i = 0; i < root.childCount; i++)
                children[i] = root.GetChild(i);
            return children;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj.AddComponent<RectTransform>();
        }

        private static RectTransform CreateBar(string name, Transform parent)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Color.black;
            return rect;
        }
    }
}
