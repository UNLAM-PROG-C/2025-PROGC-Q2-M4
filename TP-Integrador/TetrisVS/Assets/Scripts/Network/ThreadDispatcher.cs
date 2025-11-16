using System;
using System.Collections.Concurrent;
using UnityEngine;
// Dispatcher to execute actions on the main Unity thread
public class ThreadDispatcher : MonoBehaviour
{
    private static ThreadDispatcher _instance;
    public static ThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {// Create a new GameObject to hold the dispatcher
                var go = new GameObject("ThreadDispatcher");
                _instance = go.AddComponent<ThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
// Queue to hold actions to be executed on the main thread
    private readonly ConcurrentQueue<Action> _q = new ConcurrentQueue<Action>();
// Enqueue an action to be executed on the main thread
    public void Enqueue(Action a)
    {
        if (a != null) _q.Enqueue(a);
    }
// Execute queued actions each frame
    private void Update()
    {
        while (_q.TryDequeue(out var act))
        {
            try { act(); } catch (Exception ex) { Debug.LogError(ex); }
        }
    }
}