using UnityEngine;

public class PlayerInputManager : MonoBehaviour
{
    [Header("플레이어 설정")]
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f;

    [Header("카메라 설정")]
    public Transform cameraTransform;
    public float maxLookAngle = 80f;

    // 상태 관리
    private bool isInputEnabled = true;
    private bool isChatting = false;

    // 마우스 회전
    private float verticalRotation = 0;

    // 컴포넌트
    private CharacterController characterController;
    private Rigidbody rb;

    // 싱글톤 (다른 스크립트에서 접근하기 위해)
    public static PlayerInputManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 컴포넌트 찾기
        characterController = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();

        // 카메라가 설정되지 않았으면 Main Camera 찾기
        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                cameraTransform = mainCam.transform;
            }
        }

        // 마우스 커서 설정
        UpdateCursorState();
    }

    private void Update()
    {
        // 채팅 상태에 따른 입력 처리
        if (isInputEnabled && !isChatting)
        {
            HandleMovement();
            HandleMouseLook();
        }

        // ESC키로 채팅 종료
        if (isChatting && Input.GetKeyDown(KeyCode.Escape))
        {
            ChatUI chatUI = FindObjectOfType<ChatUI>();
            if (chatUI != null)
            {
                chatUI.CloseChat();
            }
        }
    }

    /// <summary>
    /// 플레이어 이동 처리
    /// </summary>
    private void HandleMovement()
    {
        // WASD 입력 받기
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 이동 벡터 계산
        Vector3 direction = new Vector3(horizontal, 0, vertical).normalized;

        if (direction.magnitude >= 0.1f)
        {
            // 카메라 방향 기준으로 이동
            Vector3 moveDirection = transform.TransformDirection(direction);

            if (characterController != null)
            {
                // CharacterController 사용
                characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
            }
            else if (rb != null)
            {
                // Rigidbody 사용
                rb.MovePosition(rb.position + moveDirection * moveSpeed * Time.deltaTime);
            }
            else
            {
                // Transform 직접 이동
                transform.Translate(moveDirection * moveSpeed * Time.deltaTime);
            }
        }
    }

    /// <summary>
    /// 마우스 시점 처리
    /// </summary>
    private void HandleMouseLook()
    {
        if (cameraTransform == null) return;

        // 마우스 입력 받기
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // 좌우 회전 (Y축)
        transform.Rotate(Vector3.up * mouseX);

        // 상하 회전 (X축) - 카메라만
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }

    /// <summary>
    /// 채팅 상태 설정
    /// </summary>
    public void SetChatting(bool chatting)
    {
        isChatting = chatting;
        UpdateCursorState();

        Debug.Log($"[PlayerInputManager] 채팅 상태: {chatting}");
    }

    /// <summary>
    /// 입력 활성화/비활성화
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        isInputEnabled = enabled;
        UpdateCursorState();

        Debug.Log($"[PlayerInputManager] 입력 상태: {enabled}");
    }

    /// <summary>
    /// 커서 상태 업데이트
    /// </summary>
    private void UpdateCursorState()
    {
        if (isChatting)
        {
            // 채팅 중: 커서 보이기, 잠금 해제
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 게임 중: 커서 숨기기, 중앙 잠금
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>
    /// 현재 채팅 상태 반환
    /// </summary>
    public bool IsChatting()
    {
        return isChatting;
    }

    /// <summary>
    /// 입력 상태 반환
    /// </summary>
    public bool IsInputEnabled()
    {
        return isInputEnabled;
    }
}