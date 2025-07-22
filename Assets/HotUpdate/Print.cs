using UnityEngine;

public class Print : MonoBehaviour
{
    private void Start()
    {
        Debug.Log($"[Print] GameObject:{name}");
    }
}