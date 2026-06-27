using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LevelEditor
{
    public class LevelEditorPaletteButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Image iconImage;
        [SerializeField] private LevelEditorVisualView visualView;

        private LevelEditorController controller;
        private int index;

        public void Initialize(LevelEditorController owner, int entryIndex, LevelEditorPaletteEntry entry)
        {
            controller = owner;
            index = entryIndex;

            if (labelText != null)
                labelText.text = entry != null ? entry.label : string.Empty;

            if (iconImage != null)
            {
                iconImage.sprite = entry != null ? entry.icon : null;
                iconImage.gameObject.SetActive(iconImage.sprite != null);
            }

            if (visualView != null && entry != null)
                visualView.SetEntry(entry, entry.defaultDirection);

            if (button == null)
                button = GetComponent<Button>();

            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
                button.onClick.AddListener(HandleClick);
            }
        }

        private void HandleClick()
        {
            if (controller != null)
                controller.SelectPalette(index);
        }
    }
}
