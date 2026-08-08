using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Assets.Scripts.View
{
    public class LobbyRowView : MonoBehaviour
    {
        public TextMeshProUGUI nameText;
        public Button kickButton;

        public void Initialize(string playerName, string hexColor, string readyStatus, bool isHost, bool amIHost, string targetToken, System.Action<string> onKickAction)
        {
            string hostTag = isHost ? "<color=yellow>[ХОСТ]</color> " : "";
            nameText.text = $"{hostTag}<color=#{hexColor}><b>{playerName}</b></color> - {readyStatus}";

            // Кнопка кика видна только Хосту и только на чужих строках
            if (kickButton != null)
            {
                kickButton.gameObject.SetActive(amIHost && !isHost);
                kickButton.onClick.RemoveAllListeners();
                kickButton.onClick.AddListener(() => onKickAction?.Invoke(targetToken));
            }
        }
    }
}