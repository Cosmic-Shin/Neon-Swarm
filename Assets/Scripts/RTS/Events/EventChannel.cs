using System;
using UnityEngine;

public abstract class EventChannel<T> : ScriptableObject
{
    public event Action<T> Raised;
    public void Raise(T value) => Raised?.Invoke(value);
}
