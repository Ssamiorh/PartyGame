using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class LobbyButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _label;

        /// <summary>
        /// Configure the button for a minigame and invoke <paramref name="onClicked"/> when pressed.
        /// </summary>
        public void Setup(E_MiniGame miniGame, Action<E_MiniGame> onClicked)
        {
            _label.text = miniGame.ToString();
            _button.onClick.AddListener(() => onClicked?.Invoke(miniGame));
        }
    }

}