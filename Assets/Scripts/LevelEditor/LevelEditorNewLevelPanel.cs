using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LevelEditor
{
    public class LevelEditorNewLevelPanel : MonoBehaviour
    {
        [SerializeField] private LevelEditorController controller;
        [SerializeField] private TMP_InputField widthInput;
        [SerializeField] private TMP_InputField heightInput;
        [SerializeField] private TMP_InputField laserMaxDistanceInput;
        [SerializeField] private TMP_InputField moveLimitInput;
        [SerializeField] private Toggle useLaserDistanceLimitToggle;

        private void Awake()
        {
            
        }

        public void CreateNewLevel()
        {
            if (controller == null)
                return;

            int width = ParseInt(widthInput, 8);
            int height = ParseInt(heightInput, 8);
            int laserMaxDistance = ParseInt(laserMaxDistanceInput, 20);
            int moveLimit = ParseInt(moveLimitInput, 0);
            bool useLaserDistanceLimit = useLaserDistanceLimitToggle != null && useLaserDistanceLimitToggle.isOn;

            controller.CreateNewLevel(width, height, laserMaxDistance, moveLimit, useLaserDistanceLimit);
        }

        private int ParseInt(TMP_InputField inputField, int defaultValue)
        {
            if (inputField == null)
                return defaultValue;

            return int.TryParse(inputField.text, out int value) ? value : defaultValue;
        }
    }
}
