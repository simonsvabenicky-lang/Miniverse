using System.Collections.Generic;
using UnityEngine;

namespace Frontline
{
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>
    /// Minimal prefab pool. Nothing gets Instantiate'd mid-run once warmed:
    /// allocation spikes are the number one source of stutter in this genre,
    /// where dozens of enemies and bullets appear and die every second.
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        readonly T _prefab;
        readonly Transform _parent;
        readonly Stack<T> _idle = new Stack<T>();

        public ObjectPool(T prefab, Transform parent, int warm = 0)
        {
            _prefab = prefab;
            _parent = parent;
            for (int i = 0; i < warm; i++)
            {
                T inst = Object.Instantiate(_prefab, _parent);
                inst.gameObject.SetActive(false);
                _idle.Push(inst);
            }
        }

        public T Get(Vector3 position)
        {
            T inst = _idle.Count > 0 ? _idle.Pop() : Object.Instantiate(_prefab, _parent);
            inst.transform.position = position;
            inst.gameObject.SetActive(true);
            if (inst is IPoolable p) p.OnSpawned();
            return inst;
        }

        public void Release(T inst)
        {
            if (inst == null || !inst.gameObject.activeSelf) return;
            if (inst is IPoolable p) p.OnDespawned();
            inst.gameObject.SetActive(false);
            _idle.Push(inst);
        }
    }
}
