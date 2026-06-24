using Data;
using Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    public class GameManager : Singleton<GameManager>
    {
        private GameProgressTracker _progressTracker;
        public GameProgressTracker ProgressTracker => _progressTracker;

        /// <summary>
        /// Track session time during gameplay
        /// </summary>
        private float _sessionStartTime = -1f;
        public float CurrentSessionLength => Time.realtimeSinceStartup - _sessionStartTime;

        

        private void Awake()
        {
            DataPersistenceManager.OnLoad += LoadData;
            DataPersistenceManager.OnSave += SaveData;
            DataPersistenceManager.OnErase += EraseSave;
        }

        private void Start()
        {
            PreferencesManager.LoadPreferencesStart();
            DataPersistenceManager.Instance.LoadGame();
        }

        /// <summary>
        /// Load data from save file, create a new save from 0 if none exists
        /// </summary>
        private void LoadData(int slot)
        {
            _progressTracker = GameProgressTracker.LoadDataFromFile(slot);

            ResetSessionTime();
        }

        /// <summary>
        /// Recreate a new save from 0
        /// </summary>
        private void EraseSave(int slot)
        {
            _progressTracker = new GameProgressTracker();

            ResetSessionTime();
        }

        private void SaveData(int slot)
        {
            //Tracked game time
            if (_sessionStartTime > 0f)
            {
                _progressTracker.AddTimePlayed(CurrentSessionLength);
            }
            //Continue to track time
            ResetSessionTime();

            _progressTracker?.Save(slot);
        }

        public void ResetSessionTime()
        {
            _sessionStartTime = Time.realtimeSinceStartup;
        }
    }

}
