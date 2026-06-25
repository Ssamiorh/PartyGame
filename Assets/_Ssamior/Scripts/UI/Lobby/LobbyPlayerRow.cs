using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One entry in the lobby player list: shows the player's name and color.
    /// The color dropdown is only shown on the local player's own row; everyone
    /// else's color is reflected as a static swatch.
    /// </summary>
    public class LobbyPlayerRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private Image _colorSwatch;
        [SerializeField] private TMP_Dropdown _colorDropdown;

        private Action<int> _onColorSelected;

        public void Setup(string displayName, int colorIndex, bool isLocalPlayer, Action<int> onColorSelected)
        {
            _nameLabel.text = displayName;
            _colorSwatch.color = PlayerColors.ColorAt(colorIndex);

            _onColorSelected = onColorSelected;

            // Only the local player can recolor themselves.
            _colorDropdown.gameObject.SetActive(isLocalPlayer);
            if (!isLocalPlayer)
                return;

            // Re-subscribe defensively in case the row is pooled/reused.
            _colorDropdown.onValueChanged.RemoveListener(HandleDropdownChanged);

            List<string> options = new(PlayerColors.Count);
            for (int i = 0; i < PlayerColors.Count; i++)
                options.Add(PlayerColors.NameAt(i));

            _colorDropdown.ClearOptions();
            _colorDropdown.AddOptions(options);
            _colorDropdown.SetValueWithoutNotify(PlayerColors.Clamp(colorIndex));

            _colorDropdown.onValueChanged.AddListener(HandleDropdownChanged);
        }

        private void HandleDropdownChanged(int index) => _onColorSelected?.Invoke(index);
    }
}
