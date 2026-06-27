using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class GameDataRegistry 
    {
        public const string ManagingSceneName = "ManagingScene";
        public const string MainMenuSceneName = "MainMenuScene";
        public const string LobbySceneName = "LobbyScene";


        public static string GetMiniGameSceneName(E_MiniGame miniGame) => $"MiniGame_{(int)miniGame:000}";
    }
}