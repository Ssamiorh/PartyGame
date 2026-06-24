using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Utils
{
#if UNITY_EDITOR
    public class EditorExtensions : Editor
    {
        [MenuItem("Ssamior/Play")]
        public static void PlayFromManagingScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene("Assets/_Ssamior/Scenes/ManagingScene.unity");
            }

            EditorApplication.isPlaying = true;
        }
    }
#endif
}