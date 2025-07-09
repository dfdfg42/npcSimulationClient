using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatUI : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    public GameObject chatPanel; // 채팅 패널
    public ScrollRect chatScrollRect; // 스크롤 영역
    public Transform chatContent; // 메시지들이 들어갈 부모 Transform
    public TMP_InputField inputField; // 메시지 입력창
    public Button sendButton; // 전송 버튼
    public Button closeButton; // 닫기 버튼
    public TextMeshProUGUI npcNameText; // NPC 이름 표시
    public TextMeshProUGUI statusText; // 상태 텍스트 (연결중, 응답 대기 등)

    [Header("메시지 프리팹")]
    public GameObject playerMessagePrefab; // 플레이어 메시지 프리팹
    public GameObject npcMessagePrefab; // NPC 메시지 프리팹
    public GameObject systemMessagePrefab; // 시스템 메시지 프리팹

    [Header("채팅 설정")]
    public int maxMessages = 50; // 최대 메시지 수
    public float autoScrollDelay = 0.1f; // 자동 스크롤 딜레이
    public bool enableTypingEffect = true; // 타이핑 효과 활성화
    public float typingSpeed = 0.05f; // 타이핑 속도

    [Header("사운드")]
    public AudioClip messageSendSound;
    public AudioClip messageReceiveSound;
    public AudioClip typingSound;

    private string currentNPCId;
    private string currentNPCName;
    private bool isWaitingForResponse = false;
    private List<GameObject> messageObjects = new List<GameObject>();
    private AudioSource audioSource;
    private Coroutine typingCoroutine;

    private void Awake()
    {
        // 오디오 소스 설정
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 초기 상태 설정
        if (chatPanel != null)
        {
            chatPanel.SetActive(false);
        }
    }

    private void Start()
    {
        // 이벤트 등록
        ChatManager.OnNPCResponse += OnNPCResponseReceived;
        ChatManager.OnConnectionError += OnConnectionError;

        // UI 이벤트 등록
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(SendMessage);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseChat);
        }

        if (inputField != null)
        {
            inputField.onEndEdit.AddListener(OnInputEndEdit);
        }

        // 상태 텍스트 초기화
        UpdateStatusText("대화를 시작하세요!");
    }

    private void Update()
    {
        // 채팅창이 열려있을 때만 처리
        if (chatPanel != null && chatPanel.activeInHierarchy)
        {
            // ESC 키로 채팅창 닫기
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseChat();
            }

            // Tab 키로 입력창 포커스 (선택사항)
            if (Input.GetKeyDown(KeyCode.Tab) && inputField != null)
            {
                inputField.Select();
                inputField.ActivateInputField();
            }
        }
    }

    private void OnDestroy()
    {
        // 이벤트 해제
        ChatManager.OnNPCResponse -= OnNPCResponseReceived;
        ChatManager.OnConnectionError -= OnConnectionError;
    }

    /// <summary>
    /// 채팅창 열기
    /// </summary>
    public void OpenChat(string npcId, string npcName)
    {
        currentNPCId = npcId;
        currentNPCName = npcName;

        // UI 활성화
        if (chatPanel != null)
        {
            chatPanel.SetActive(true);
        }

        // NPC 이름 표시
        if (npcNameText != null)
        {
            npcNameText.text = npcName;
        }

        // 플레이어 입력 제어 (채팅 모드로 전환)
        PlayerInputManager playerInput = PlayerInputManager.Instance;
        if (playerInput != null)
        {
            playerInput.SetChatting(true);
        }

        // 입력창에 포커스 (약간 지연 후)
        StartCoroutine(FocusInputFieldDelayed());

        // 상태 업데이트
        UpdateStatusText($"{npcName}와 대화 중...");

        // 시스템 메시지 추가
        AddSystemMessage($"{npcName}와의 대화가 시작되었습니다.");

        Debug.Log($"[ChatUI] {npcName}와의 채팅창이 열렸습니다.");
    }

    /// <summary>
    /// 입력창 포커스 (지연)
    /// </summary>
    private IEnumerator FocusInputFieldDelayed()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        if (inputField != null)
        {
            inputField.Select();
            inputField.ActivateInputField();
        }
    }

    /// <summary>
    /// 채팅창 닫기
    /// </summary>
    public void CloseChat()
    {
        if (chatPanel != null)
        {
            chatPanel.SetActive(false);
        }

        // NPC 컨트롤러에 대화 종료 알림
        NPCController[] npcs = FindObjectsOfType<NPCController>();
        foreach (var npc in npcs)
        {
            if (npc.npcId == currentNPCId)
            {
                npc.EndConversation();
                break;
            }
        }

        // 타이핑 효과 중단
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        Debug.Log($"[ChatUI] 채팅창이 닫혔습니다.");
    }

    /// <summary>
    /// 메시지 전송
    /// </summary>
    public void SendMessage()
    {
        if (inputField == null || string.IsNullOrWhiteSpace(inputField.text) || isWaitingForResponse)
        {
            return;
        }

        string message = inputField.text.Trim();
        inputField.text = "";

        // 플레이어 메시지 추가
        AddPlayerMessage(message);

        // 서버에 메시지 전송
        isWaitingForResponse = true;
        UpdateStatusText("응답을 기다리는 중...");

        ChatManager.Instance.SendMessageToNPC(currentNPCId, message);

        // 사운드 재생
        PlaySound(messageSendSound);

        // 입력창에 다시 포커스 (더 강력하게)
        StartCoroutine(RefocusInputFieldStrong());
    }

    /// <summary>
    /// 강력한 입력창 재포커스
    /// </summary>
    private IEnumerator RefocusInputFieldStrong()
    {
        yield return new WaitForEndOfFrame();

        if (inputField != null && chatPanel.activeInHierarchy)
        {
            inputField.text = ""; // 혹시 남은 텍스트 제거
            inputField.Select();
            inputField.ActivateInputField();

            // 한 프레임 더 대기 후 다시 시도
            yield return new WaitForEndOfFrame();
            inputField.Select();
        }
    }

    /// <summary>
    /// 입력 완료 시 호출 (엔터키)
    /// </summary>
    private void OnInputEndEdit(string value)
    {
        // Enter 키가 눌렸고, 채팅창이 활성화되어 있을 때만
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            && chatPanel.activeInHierarchy)
        {
            SendMessage();

            // 포커스를 다시 입력창으로
            StartCoroutine(RefocusInputField());
        }
    }

    /// <summary>
    /// NPC 응답 받았을 때
    /// </summary>
    private void OnNPCResponseReceived(string npcId, string response)
    {
        if (npcId != currentNPCId) return;

        isWaitingForResponse = false;
        UpdateStatusText($"{currentNPCName}와 대화 중...");

        // NPC 메시지 추가
        if (enableTypingEffect)
        {
            AddNPCMessageWithTyping(response);
        }
        else
        {
            AddNPCMessage(response);
        }

        // 사운드 재생
        PlaySound(messageReceiveSound);
    }

    /// <summary>
    /// 연결 오류 발생 시
    /// </summary>
    private void OnConnectionError(string error)
    {
        isWaitingForResponse = false;
        UpdateStatusText("연결 오류가 발생했습니다.");
        AddSystemMessage($"오류: {error}");
    }

    /// <summary>
    /// 플레이어 메시지 추가
    /// </summary>
    private void AddPlayerMessage(string message)
    {
        if (playerMessagePrefab == null || chatContent == null) return;

        GameObject messageObj = Instantiate(playerMessagePrefab, chatContent);

        // 메시지 텍스트 설정
        TextMeshProUGUI messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();
        if (messageText != null)
        {
            messageText.text = message;
        }

        AddMessageToList(messageObj);
        ScrollToBottom();
    }

    /// <summary>
    /// NPC 메시지 추가
    /// </summary>
    private void AddNPCMessage(string message)
    {
        if (npcMessagePrefab == null || chatContent == null) return;

        GameObject messageObj = Instantiate(npcMessagePrefab, chatContent);

        // 메시지 텍스트 설정
        TextMeshProUGUI messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();
        if (messageText != null)
        {
            messageText.text = message;
        }

        AddMessageToList(messageObj);
        ScrollToBottom();
    }

    /// <summary>
    /// 타이핑 효과와 함께 NPC 메시지 추가
    /// </summary>
    private void AddNPCMessageWithTyping(string message)
    {
        if (npcMessagePrefab == null || chatContent == null) return;

        GameObject messageObj = Instantiate(npcMessagePrefab, chatContent);
        TextMeshProUGUI messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();

        if (messageText != null)
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }
            typingCoroutine = StartCoroutine(TypeMessage(messageText, message));
        }

        AddMessageToList(messageObj);
        ScrollToBottom();
    }

    /// <summary>
    /// 시스템 메시지 추가
    /// </summary>
    private void AddSystemMessage(string message)
    {
        if (systemMessagePrefab == null || chatContent == null) return;

        GameObject messageObj = Instantiate(systemMessagePrefab, chatContent);

        // 메시지 텍스트 설정
        TextMeshProUGUI messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();
        if (messageText != null)
        {
            messageText.text = message;
        }

        AddMessageToList(messageObj);
        ScrollToBottom();
    }

    /// <summary>
    /// 타이핑 효과 코루틴
    /// </summary>
    private IEnumerator TypeMessage(TextMeshProUGUI textComponent, string fullMessage)
    {
        textComponent.text = "";

        for (int i = 0; i <= fullMessage.Length; i++)
        {
            textComponent.text = fullMessage.Substring(0, i);

            // 타이핑 사운드 재생
            if (i < fullMessage.Length && typingSound != null)
            {
                PlaySound(typingSound);
            }

            yield return new WaitForSeconds(typingSpeed);
        }

        typingCoroutine = null;
    }

    /// <summary>
    /// 메시지를 리스트에 추가하고 최대 개수 관리
    /// </summary>
    private void AddMessageToList(GameObject messageObj)
    {
        messageObjects.Add(messageObj);

        // 최대 메시지 수 초과 시 오래된 메시지 삭제
        while (messageObjects.Count > maxMessages)
        {
            GameObject oldMessage = messageObjects[0];
            messageObjects.RemoveAt(0);
            if (oldMessage != null)
            {
                Destroy(oldMessage);
            }
        }
    }

    /// <summary>
    /// 스크롤을 맨 아래로 이동
    /// </summary>
    private void ScrollToBottom()
    {
        if (chatScrollRect != null)
        {
            StartCoroutine(ScrollToBottomCoroutine());
        }
    }

    /// <summary>
    /// 스크롤 이동 코루틴
    /// </summary>
    private IEnumerator ScrollToBottomCoroutine()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(autoScrollDelay);

        if (chatScrollRect != null)
        {
            chatScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    /// <summary>
    /// 입력창에 다시 포커스 (기본)
    /// </summary>
    private IEnumerator RefocusInputField()
    {
        yield return new WaitForEndOfFrame();

        if (inputField != null && chatPanel.activeInHierarchy)
        {
            inputField.Select();
            inputField.ActivateInputField();
        }
    }

    /// <summary>
    /// 상태 텍스트 업데이트
    /// </summary>
    private void UpdateStatusText(string status)
    {
        if (statusText != null)
        {
            statusText.text = status;
        }
    }

    /// <summary>
    /// 사운드 재생
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// 모든 메시지 삭제
    /// </summary>
    public void ClearAllMessages()
    {
        foreach (GameObject messageObj in messageObjects)
        {
            if (messageObj != null)
            {
                Destroy(messageObj);
            }
        }
        messageObjects.Clear();
    }
}