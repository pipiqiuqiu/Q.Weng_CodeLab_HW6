using UnityEngine;

[DefaultExecutionOrder(100)]
public class cameraFollow : MonoBehaviour
{
    public Transform player;

    void LateUpdate()
    {
        if (player == null) return;

        transform.position = new Vector3(
            player.position.x,
            transform.position.y,
            player.position.z
        );
    }
}
