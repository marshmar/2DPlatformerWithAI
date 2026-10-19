using System.Collections.Generic;
using UnityEngine;

namespace Platformer
{
    /// <summary>이펙트·총알처럼 자주 생겼다 사라지는 오브젝트를 재사용한다. 씬이 바뀌면 자동으로 비운다.</summary>
    public static class FXPool
    {
        static readonly Dictionary<GameObject, Queue<GameObject>> pools = new();
        static readonly Dictionary<GameObject, GameObject> prefabOf = new();
        static Transform root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            pools.Clear();
            prefabOf.Clear();
            root = null;
        }

        public static GameObject Spawn(GameObject prefab, Vector2 position)
        {
            if (root == null)
            {
                // 씬이 바뀌어 루트가 사라졌으면 그 안의 풀 오브젝트도 사라졌으니 기록을 비운다
                pools.Clear();
                prefabOf.Clear();
                root = new GameObject("[FX Pool]").transform;
            }

            if (!pools.TryGetValue(prefab, out Queue<GameObject> pool))
                pools[prefab] = pool = new Queue<GameObject>();

            GameObject instance = pool.Count > 0 ? pool.Dequeue() : Object.Instantiate(prefab, root);
            prefabOf[instance] = prefab;
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            instance.transform.localScale = prefab.transform.localScale;
            instance.SetActive(true);
            return instance;
        }

        public static void Release(GameObject instance)
        {
            instance.SetActive(false);
            if (prefabOf.TryGetValue(instance, out GameObject prefab) && pools.TryGetValue(prefab, out Queue<GameObject> pool))
                pool.Enqueue(instance);
            else
                Object.Destroy(instance);
        }
    }
}
