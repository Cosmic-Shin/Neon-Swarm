using UnityEngine;
using UnityEngine.UI;

// "POWER used/made" next to the credits; turns red when low (towers offline, production at half speed).
public class PowerDisplay : MonoBehaviour
{
    [SerializeField] Text label;
    [SerializeField] Color normal = new(0.55f, 1f, 0.2f);
    [SerializeField] Color low = new(1f, 0.2f, 0.25f);

    void LateUpdate()
    {
        int used = Structure.PowerUsed, made = Structure.PowerMade;
        label.text = used > made ? $"LOW POWER  {used}/{made}" : $"POWER  {used}/{made}";
        label.color = used > made ? low : normal;
    }
}
