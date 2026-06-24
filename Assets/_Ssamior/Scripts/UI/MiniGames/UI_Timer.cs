using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Drop-in timer label for any minigame. Shows the play timer while the game is
    /// running, then the return-to-lobby countdown once it finishes. Add it to a UI
    /// GameObject in the minigame scene and assign the label — no per-game code needed.
    /// </summary>
    public class UI_Timer : MonoBehaviour
    {
        [SerializeField] private MiniGameController _controller;
        [SerializeField] private TextMeshProUGUI _label;

        private void Update()
        {
            bool showReturn = _controller.State == E_MiniGameState.Finished;
            bool showTimer = _controller.State == E_MiniGameState.Playing && _controller.HasTimer;

            _label.enabled = showTimer || showReturn;
            if (!_label.enabled)
                return;

            float seconds = showReturn ? _controller.ReturnCountdownRemaining : _controller.TimeRemaining;
            _label.text = Format(seconds);
        }

        private static string Format(float seconds)
        {
            int total = Mathf.CeilToInt(seconds);
            return $"{total / 60:0}:{total % 60:00}";
        }
    }
}
