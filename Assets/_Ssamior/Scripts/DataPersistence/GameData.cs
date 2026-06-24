using MessagePack;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Data
{
    /// <summary>
    /// Handle the save of any structure implementing this class
    /// </summary>
    public abstract class GameData<T> where T: class, new()
    {
        protected static string FileName = typeof(T).Name;

        public abstract void Save(int slot);

        public static T LoadDataFromFile(int slot)
        {
            if (!File.Exists(DataPersistenceManager.GetSaveFolder(slot) + FileName))
            {   
                return new T();
            }

            var data = File.ReadAllBytes(DataPersistenceManager.GetSaveFolder(slot) + FileName);
            Debug.Log($"Data loaded file:{FileName} data:{typeof(T)}:\n{MessagePackSerializer.ConvertToJson(data)}");
            return MessagePackSerializer.Deserialize<T>(data);
        }


        public static void SaveData(T data, int slot)
        {
            byte[] bytes = MessagePackSerializer.Serialize(data);

            // write bytes to file
            FileStream file = File.OpenWrite(DataPersistenceManager.GetSaveFolder(slot) + FileName);
            file.Write(bytes, 0, bytes.Length);
            file.Close();

            Debug.Log($"Data saved: file:{FileName} data:{MessagePackSerializer.ConvertToJson(bytes)}");
            //Debug.Log($"{typeof(T)} has saved data at {SaveFolder + FileName}");
            //Debug.Log($"Saved Data : {MessagePackSerializer.ConvertToJson(bytes)}");
        }

        public static bool DoSaveExists(int slot)
        {
            return File.Exists(DataPersistenceManager.GetSaveFolder(slot) + FileName);
        }
    }
}

