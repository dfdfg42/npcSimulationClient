using UnityEngine;

public class Billboard : MonoBehaviour
{
    public Camera targetCamera;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (targetCamera != null)
        {
            // 카메라를 향하도록 회전
            transform.LookAt(targetCamera.transform);
            // Y축만 회전하게 하려면 아래 코드 사용
            // Vector3 direction = targetCamera.transform.position - transform.position;
            // direction.y = 0;
            // transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}