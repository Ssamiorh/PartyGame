using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Data
{
    public class DataPersistenceManager : Singleton<DataPersistenceManager>
    {
        // Events
        public static event Action<int> OnLoad;
        public static event Action<int> OnSave;
        public static event Action<int> OnErase;

        public static int DataVersion = 20260324;

        [Header("File saving")]
        [SerializeField] private bool _useEncryption;

        private int _chosenSaveSlot = -1;
        public bool IsInitialized => _chosenSaveSlot != -1;

        [Header("AutoSave")]
        [SerializeField] private bool _autoSave;
        private float _autoSaveDelay;
        private Coroutine _autoSaveCoroutine;

        public static string GetSaveFolder(int slot)
        {
            if (!Directory.Exists(Application.persistentDataPath + "/Saves/Save" + slot + "/"))
            {
                Directory.CreateDirectory(Application.persistentDataPath + "/Saves/Save" + slot + "/");
            }
            return Application.persistentDataPath + "/Saves/Save" + slot + "/";
        }


        public static void DeleteSave(int slot)
        {
            string folderPath = GetSaveFolder(slot);

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    Debug.LogWarning($"Save folder not found: {folderPath}");
                    return;
                }

                // Delete all files in the folder
                string[] files = Directory.GetFiles(folderPath);
                foreach (var file in files)
                {
                    File.Delete(file);
                    Debug.Log($"Deleted file: {file}");
                }

                Debug.Log($"All files deleted from slot {slot} (folder retained).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete slot {slot} files: {ex.Message}");
            }
        }

        public void SelectSaveSlot(int slot)
        {
            if(slot > 3 || slot < 1)
            {
                Debug.LogError($"Cannot select save slot {slot}");
                _chosenSaveSlot = 1;
                return;
            }
            _chosenSaveSlot = slot;
        }

        /// <summary>
        /// Ask all scripts to save their data
        /// </summary>
        public void SaveGame()
        {
            if (_chosenSaveSlot > 3 || _chosenSaveSlot < 1)
            {
                Debug.LogError($"Cannot save slot {_chosenSaveSlot}");
                return;
            }
            // Tell data persistence objects to save their data
            OnSave?.Invoke(_chosenSaveSlot);
        }

        /// <summary>
        /// Erase all data, start with a new empty save
        /// </summary>
        public void EraseSave()
        {
            OnErase?.Invoke(_chosenSaveSlot);
            SaveGame();
        }

        /// <summary>
        /// Load all stats from the save file
        /// </summary>
        public void LoadGame()
        {
            if(_chosenSaveSlot == -1)
            {
                _chosenSaveSlot = 1;
            }
            Debug.Log($"LoadGame slot:{_chosenSaveSlot}");
            //Load data from save file using data handler
            OnLoad?.Invoke(_chosenSaveSlot);
        }

        /// <summary>
        /// Auto save coroutine
        /// </summary>
        /// <returns></returns>
        private IEnumerator AutoSaveCoroutine()
        {
           WaitForSeconds delay = new(_autoSaveDelay);
           while(true)
           {
               yield return delay;

               //Check if auto save was stopped
               if (!_autoSave)
                   StopCoroutine(_autoSaveCoroutine);

               SaveGame();
           }
        }

        public void ToggleAutoSave(bool autoSave)
        {
           if (this._autoSave == autoSave)
               return;

           this._autoSave = autoSave;

           if (autoSave)
           {
               StopCoroutine(_autoSaveCoroutine);
               _autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
           }
           else
           {
               StopCoroutine(_autoSaveCoroutine);
           }
        }
    }
}