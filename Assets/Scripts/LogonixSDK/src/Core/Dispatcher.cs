using System;
using System.Collections.Generic;
using UnityEngine;

namespace LogonixSDK.src.Core
{
    public class Dispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> ExecutionQueue = new();

        private void Update()
        {
            lock (ExecutionQueue)
            {
                while (ExecutionQueue.Count > 0)
                {
                    ExecutionQueue.Dequeue().Invoke();
                }
            }
        }

        public static void Enqueue(Action action)
        {
            if (action == null) return;
            lock (ExecutionQueue)
            {
                ExecutionQueue.Enqueue(action);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnLoad()
        {
            var go = new GameObject("LogonixSDK.Dispatcher");
            DontDestroyOnLoad(go);
            go.AddComponent<Dispatcher>();
        }
    }
}