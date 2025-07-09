using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class NPCController : MonoBehaviour
{
    [Header("NPC 정보")]
    public string npcId = "seoa";
    public string npcName = "이서아";
    [TextArea(3, 5)]
    public string npcPersona = "21살의 대학생. 시각 디자인을 전공하며 졸업 작품으로 고민이 많다.";

    [Header("상호작용 설정")]
    public float interactionDistance = 3.0f;
    public KeyCode interactionKey = KeyCode.E;
    public LayerMask playerLayerMask = 1; // Player 레이어

    [Header("UI 표시")]
    public GameObject interactionPrompt; // "E키를 눌러 대화" UI
    public Canvas worldCanvas; // 월드 스페이스 캔버스 (선택사항)

    [Header("시각적 효과")]
    public GameObject highlightEffect; // 하이라이트 효과 (선택사항)
    public AudioClip interactionSound; // 상호작용 사운드 (선택사항)

    private Transform player;
    private bool playerInRange = false;
    private bool isTalking = false;
    private AudioSource audioSource;
    private Camera mainCamera;

    // 컴포넌트
    private Collider npcCollider;

    private void Start()
    {
        // 컴포넌트 초기화
        npcCollider = GetComponent<Collider>();
        npcCollider.isTrigger = true; // 트리거로 설정

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && interactionSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        mainCamera = Camera.main;

        // Player 찾기
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        // UI 초기화
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(false);
        }

        if (highlightEffect != null)
        {
            highlightEffect.SetActive(false);
        }

        // 월드 캔버스가 있으면 카메라를 향하도록 설정
        if (worldCanvas != null)
        {
            worldCanvas.worldCamera = mainCamera;
        }
    }

    private void Update()
    {
        // Player 거리 체크
        CheckPlayerDistance();

        // 상호작용 키 입력 체크
        if (playerInRange && Input.GetKeyDown(interactionKey) && !isTalking)
        {
            StartConversation();
        }

        // 월드 캔버스가 있으면 항상 카메라를 향하도록
        if (worldCanvas != null && mainCamera != null)
        {
            worldCanvas.transform.LookAt(worldCanvas.transform.position + mainCamera.transform.rotation * Vector3.forward,
                                        mainCamera.transform.rotation * Vector3.up);
        }
    }

    /// <summary>
    /// 플레이어와의 거리 체크
    /// </summary>
    private void CheckPlayerDistance()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        bool wasInRange = playerInRange;
        playerInRange = distance <= interactionDistance;

        // 상태 변화 시 UI 업데이트
        if (wasInRange != playerInRange)
        {
            UpdateInteractionUI();
        }
    }

    /// <summary>
    /// 상호작용 UI 업데이트
    /// </summary>
    private void UpdateInteractionUI()
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(playerInRange && !isTalking);
        }

        if (highlightEffect != null)
        {
            highlightEffect.SetActive(playerInRange && !isTalking);
        }
    }

    /// <summary>
    /// 대화 시작
    /// </summary>
    public void StartConversation()
    {
        if (isTalking) return;

        Debug.Log($"[NPCController] {npcName}와 대화를 시작합니다.");

        // 사운드 재생
        if (audioSource != null && interactionSound != null)
        {
            audioSource.PlayOneShot(interactionSound);
        }

        // 대화 상태로 변경
        isTalking = true;
        UpdateInteractionUI();

        // 채팅 UI 열기 (더 상세한 디버그)
        Debug.Log("[NPCController] ChatUI를 찾는 중...");

        ChatUI[] allChatUIs = FindObjectsOfType<ChatUI>(true); // 비활성화된 것도 포함
        Debug.Log($"[NPCController] 발견된 ChatUI 개수: {allChatUIs.Length}");

        ChatUI chatUI = null;
        if (allChatUIs.Length > 0)
        {
            chatUI = allChatUIs[0];
            Debug.Log($"[NPCController] ChatUI 발견: {chatUI.gameObject.name}");
        }

        if (chatUI != null)
        {
            Debug.Log($"[NPCController] ChatUI.OpenChat() 호출 - NPC ID: {npcId}, Name: {npcName}");
            chatUI.OpenChat(npcId, npcName);
        }
        else
        {
            Debug.LogError("[NPCController] ChatUI를 찾을 수 없습니다! ChatPanel에 ChatUI 스크립트가 추가되어 있는지 확인하세요.");

            // ChatManager도 확인
            ChatManager chatManager = FindObjectOfType<ChatManager>();
            if (chatManager == null)
            {
                Debug.LogError("[NPCController] ChatManager도 찾을 수 없습니다!");
            }
            else
            {
                Debug.Log("[NPCController] ChatManager는 발견됨.");
            }
        }
    }

    /// <summary>
    /// 대화 종료
    /// </summary>
    public void EndConversation()
    {
        Debug.Log($"[NPCController] {npcName}와의 대화가 종료되었습니다.");

        isTalking = false;
        UpdateInteractionUI();
    }

    /// <summary>
    /// 마우스 클릭으로도 상호작용 가능
    /// </summary>
    private void OnMouseDown()
    {
        if (playerInRange && !isTalking)
        {
            StartConversation();
        }
    }

    /// <summary>
    /// 트리거 영역 진입
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
        {
            Debug.Log($"[NPCController] 플레이어가 {npcName} 근처에 왔습니다.");
            player = other.transform;
        }
    }

    /// <summary>
    /// 트리거 영역 탈출
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            Debug.Log($"[NPCController] 플레이어가 {npcName}에게서 멀어졌습니다.");

            // 대화 중이었다면 강제 종료
            if (isTalking)
            {
                ChatUI chatUI = FindObjectOfType<ChatUI>();
                if (chatUI != null)
                {
                    chatUI.CloseChat();
                }
            }
        }
    }

    /// <summary>
    /// Player인지 확인
    /// </summary>
    private bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") || (playerLayerMask.value & (1 << other.gameObject.layer)) > 0;
    }

    /// <summary>
    /// 디버그용 기즈모 그리기
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 상호작용 거리 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);

        // NPC 정보 표시
        Gizmos.color = Color.white;
        Vector3 labelPos = transform.position + Vector3.up * 2.5f;

#if UNITY_EDITOR
        UnityEditor.Handles.Label(labelPos, $"{npcName} (ID: {npcId})");
#endif
    }

    /// <summary>
    /// NPC 정보 설정 (런타임에서 변경 가능)
    /// </summary>
    public void SetNPCInfo(string id, string name, string persona)
    {
        npcId = id;
        npcName = name;
        npcPersona = persona;

        Debug.Log($"[NPCController] NPC 정보가 업데이트되었습니다: {name} (ID: {id})");
    }
}