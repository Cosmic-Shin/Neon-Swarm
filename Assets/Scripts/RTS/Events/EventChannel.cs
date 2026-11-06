using System;
using UnityEngine;

// ScriptableObject event channel: the asset is the event. Senders call Raise, listeners subscribe to
// Raised; neither needs a reference to the other, only to the same channel asset.
public abstract class EventChannel<T> : ScriptableObject
{
    public event Action<T> Raised;
    public void Raise(T value) => Raised?.Invoke(value);
}
