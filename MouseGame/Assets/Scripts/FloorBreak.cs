using System;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class FloorBreak : MonoBehaviour
{
    public static event Action FloorDestroyed;

    public bool IsBreaking => started;

    private bool started;
    private bool destroyed;

    private void OnEnable()
    {
        started = false;
        destroyed = false;
        GetComponent<SpriteRenderer>().enabled = false;
    }

    public void PlayBreak()
    {
        if (started) return;
        started = true;

        transform.parent.GetComponent<MeshRenderer>().enabled = false;
        GetComponent<SpriteRenderer>().enabled = true;
        GetComponent<Animator>().SetTrigger("Break");
    }

    public void DestroyFloor()
    {
        if (!started || destroyed) return;
        destroyed = true;
        FloorDestroyed?.Invoke();
        Destroy(transform.parent.gameObject);
    }
}
