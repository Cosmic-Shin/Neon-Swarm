using UnityEngine;

// Data Siphon: a slow, steady trickle of credits. Half rate while low on power.
public class Siphon : MonoBehaviour
{
    [SerializeField] int amount = 20;
    [SerializeField] float interval = 4f;

    float next;

    void OnEnable() => next = Time.time + interval; // starts counting once construction finishes

    void Update()
    {
        if (Time.time < next) return;
        next = Time.time + interval;
        ResourceBank.GetOrCreate().Add(Structure.LowPower ? amount / 2 : amount);
    }
}
