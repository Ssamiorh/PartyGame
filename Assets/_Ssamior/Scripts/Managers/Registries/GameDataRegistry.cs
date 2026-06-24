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

        public const string TagPlatformerSceneName = "MiniGame_TagPlatformer";

        // Maps each minigame to its scene name. Keep in sync with the scenes in Build Settings.
        private static readonly Dictionary<E_MiniGame, string> _miniGameSceneNames = new()
        {
            { E_MiniGame.TagPlatformer, TagPlatformerSceneName },
        };

        public static string GetMiniGameSceneName(E_MiniGame miniGame) => _miniGameSceneNames[miniGame];
    }
}