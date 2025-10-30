using System;
using System.Collections.Concurrent;
using UnityEngine;

public class ThreadDispatcher : MonoBehaviour
{
    private static ThreadDispatcher _instance;
    public static ThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("ThreadDispatcher");
                _instance = go.AddComponent<ThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private readonly ConcurrentQueue<Action> _q = new ConcurrentQueue<Action>();

    public void Enqueue(Action a)
    {
        if (a != null) _q.Enqueue(a);
    }

    private void Update()
    {
        while (_q.TryDequeue(out var act))
        {
            try { act(); } catch (Exception ex) { Debug.LogError(ex); }
        }
    }
}