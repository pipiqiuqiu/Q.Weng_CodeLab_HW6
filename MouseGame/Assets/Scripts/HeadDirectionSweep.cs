using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public sealed class HeadDirectionSweep : MonoBehaviour
{
    private Transform head;
    private Camera viewCamera;
    private float radius;
    private float height;
    
    public float CurrentAngle { get; private set; } = 90f;
    public Vector3 CurrentDirection => transform.forward;

    private void OnEnable()
    {
        head = transform.parent;
        Vector3 offset = transform.localPosition;
        radius = new Vector2(offset.x, offset.z).magnitude;
        height = offset.y;
        SetAngle(0f);
    }

    private void LateUpdate()
    {
        if (head == null || Mouse.current == null)
            return;

        if (viewCamera == null)
            viewCamera = Camera.main;
        if (viewCamera == null)
            return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = viewCamera.ScreenPointToRay(mousePosition);
        Plane headPlane = new Plane(Vector3.up, head.position);
        if (!headPlane.Raycast(ray, out float distance))
            return;

        Vector3 localDirection = head.InverseTransformDirection(ray.GetPoint(distance) - head.position);
        localDirection.y = 0f;
        if (localDirection.sqrMagnitude < 0.000001f)
            return;

        float mouseAngle = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        SetAngle(Mathf.Clamp(mouseAngle, -90f, 90f));
    }

    private void SetAngle(float angle)
    {
        CurrentAngle = angle + 90f;
        float radians = angle * Mathf.Deg2Rad;

        transform.localPosition = new Vector3(
            Mathf.Sin(radians) * radius,
            height,
            Mathf.Cos(radians) * radius);
        transform.localRotation = Quaternion.Euler(0f, angle, 0f);
    }
}
