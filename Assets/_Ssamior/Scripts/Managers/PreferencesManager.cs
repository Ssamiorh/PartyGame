using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Utils
{
    public class PreferencesManager
    {
        //Sound & Music
        public const string masterVolume_PlayerPrefKey = "masterVol";
        public const string musicVolume_PlayerPrefKey = "musicVol";
        public const string sfxVolume_PlayerPrefKey = "sfxVol";
        public const string uiVolume_PlayerPrefKey = "uiVol";

        //Player
        public const string playerColor_PlayerPrefKey = "playerColor";

        /// <summary>
        /// Use saved PlayerPrefs to update all settings
        /// </summary>
        public static void LoadPreferencesStart()
        {
            //Sound settings
            SoundManager.Instance.UpdateVolumes();
        }
    }
}