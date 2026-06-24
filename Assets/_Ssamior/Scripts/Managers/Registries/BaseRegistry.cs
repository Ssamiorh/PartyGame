using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Data
{
    public abstract class BaseRegistry<T> : ScriptableObject
        where T : ScriptableObject
    {
        // Serialized list of all elements in the registry
        [SerializeField] protected List<T> _elementsList;

        public IReadOnlyList<T> Elements => _elementsList;

        public int Count => _elementsList != null ? _elementsList.Count : 0;
        public bool IsEmpty => Count == 0;

        public T GetRandomElement()
        {
            if (IsEmpty)
                throw new InvalidOperationException($"Cannot get random element from empty {nameof(BaseRegistry<T>)}.");

            return _elementsList[UnityEngine.Random.Range(0, Count)];
        }
    }
}