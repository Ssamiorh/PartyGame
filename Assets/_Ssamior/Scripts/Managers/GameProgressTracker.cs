using System;
using Data;
using MessagePack;
using UnityEngine;

namespace Game
{
    [Serializable]
    [MessagePackObject]
    public class GameProgressTracker : GameData<GameProgressTracker>
    {
        private float _timePlayed;
        [Key(0)]
        public float TimePlayed => _timePlayed;


        [SerializationConstructor]
        public GameProgressTracker(
            float timePlayed
            )
        {
            this._timePlayed = timePlayed;
        }

        public GameProgressTracker()
        {
            _timePlayed = 0f;
        }

        public override void Save(int slot)
        {
            //Custom logic on save
            //If components need to be checked, data collected from somewhere

            SaveData(this, slot);
        }

        public void AddTimePlayed(float addedTimePlayed)
        {
            _timePlayed += addedTimePlayed;

            //LOG
            TimeSpan timeSpan = TimeSpan.FromSeconds(_timePlayed);
            Debug.Log($"TimeSpent saved : total {timeSpan.ToString(@"hh\:mm\:ss")}");
        }
    }

}