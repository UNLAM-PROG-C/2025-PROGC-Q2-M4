using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// Dispatcher to execute actions on the main Unity thread.
/// </summary>
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

  private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

  public void Enqueue(Action action)
  {
    if (action != null)
    {
      _queue.Enqueue(action);
    }
  }

  private void Update()
  {
    while (_queue.TryDequeue(out var act))
    {
      try
      {
        act();
      }
      catch (Exception ex)
      {
        Debug.LogError(ex);
      }
    }
  }
}