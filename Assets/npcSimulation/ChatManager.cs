using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class ChatRequest
{
    public string npc_id;
    public string player_message;
    public string player_name;
}

[Serializable]
public class ChatResponse
{
    public string npc_id;
    public string npc_name;
    public string npc_response;
    public string status;
}

[Serializable]
public class NPCInfo
{
    public string npc_id;
    public string name;
    public string persona;
}

[Serializable]
public class NPCListResponse
{
    public NPCInfo[] npcs;
    public int total_count;
}

public class ChatManager : MonoBehaviour
{
    [Header("서버 설정")]
    public string serverURL = "http://localhost:8000";

    [Header("디버그")]
    public bool enableDebugLogs = true;

    // 이벤트
    public static event Action<string, string> OnNPCResponse; // NPC ID, 응답 메시지
    public static event Action<string> OnConnectionError; // 오류 메시지
    public static event Action<NPCInfo[]> OnNPCListReceived; // NPC 목록

    private static ChatManager instance;
    public static ChatManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ChatManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("ChatManager");
                    instance = go.AddComponent<ChatManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 서버 연결 테스트
        StartCoroutine(TestConnection());
    }

    /// <summary>
    /// 서버 연결 테스트
    /// </summary>
    private IEnumerator TestConnection()
    {
        DebugLog("서버 연결을 테스트합니다...");

        using (UnityWebRequest request = UnityWebRequest.Get($"{serverURL}/"))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                DebugLog($"서버 연결 실패: {request.error}");
                OnConnectionError?.Invoke($"서버에 연결할 수 없습니다: {request.error}");
            }
            else
            {
                DebugLog("서버 연결 성공!");
                // NPC 목록 불러오기
                GetNPCList();
            }
        }
    }

    /// <summary>
    /// NPC와 대화하기
    /// </summary>
    public void SendMessageToNPC(string npcId, string message, string playerName = "Player")
    {
        StartCoroutine(SendChatRequest(npcId, message, playerName));
    }

    /// <summary>
    /// 채팅 요청 코루틴
    /// </summary>
    private IEnumerator SendChatRequest(string npcId, string message, string playerName)
    {
        DebugLog($"NPC '{npcId}'에게 메시지 전송: {message}");

        // 요청 데이터 생성
        ChatRequest chatRequest = new ChatRequest
        {
            npc_id = npcId,
            player_message = message,
            player_name = playerName
        };

        string jsonData = JsonUtility.ToJson(chatRequest);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        // HTTP POST 요청
        using (UnityWebRequest request = new UnityWebRequest($"{serverURL}/chat", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"채팅 요청 실패: {request.error}";
                DebugLog(errorMsg);
                OnConnectionError?.Invoke(errorMsg);
            }
            else
            {
                try
                {
                    string responseText = request.downloadHandler.text;
                    ChatResponse response = JsonUtility.FromJson<ChatResponse>(responseText);

                    DebugLog($"NPC 응답 받음: {response.npc_response}");
                    OnNPCResponse?.Invoke(response.npc_id, response.npc_response);
                }
                catch (Exception e)
                {
                    string errorMsg = $"응답 파싱 오류: {e.Message}";
                    DebugLog(errorMsg);
                    OnConnectionError?.Invoke(errorMsg);
                }
            }
        }
    }

    /// <summary>
    /// NPC 목록 가져오기
    /// </summary>
    public void GetNPCList()
    {
        StartCoroutine(GetNPCListCoroutine());
    }

    /// <summary>
    /// NPC 목록 가져오기 코루틴
    /// </summary>
    private IEnumerator GetNPCListCoroutine()
    {
        DebugLog("NPC 목록을 요청합니다...");

        using (UnityWebRequest request = UnityWebRequest.Get($"{serverURL}/npc/list"))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"NPC 목록 요청 실패: {request.error}";
                DebugLog(errorMsg);
                OnConnectionError?.Invoke(errorMsg);
            }
            else
            {
                try
                {
                    string responseText = request.downloadHandler.text;
                    NPCListResponse response = JsonUtility.FromJson<NPCListResponse>(responseText);

                    DebugLog($"NPC 목록 받음: {response.total_count}개");
                    OnNPCListReceived?.Invoke(response.npcs);
                }
                catch (Exception e)
                {
                    string errorMsg = $"NPC 목록 파싱 오류: {e.Message}";
                    DebugLog(errorMsg);
                    OnConnectionError?.Invoke(errorMsg);
                }
            }
        }
    }

    /// <summary>
    /// 새 NPC 생성
    /// </summary>
    public void CreateNPC(string npcId, string name, string persona)
    {
        StartCoroutine(CreateNPCCoroutine(npcId, name, persona));
    }

    /// <summary>
    /// NPC 생성 코루틴
    /// </summary>
    private IEnumerator CreateNPCCoroutine(string npcId, string name, string persona)
    {
        DebugLog($"새 NPC 생성: {name} (ID: {npcId})");

        // JSON 문자열 수동 생성 (Unity JsonUtility 호환성 문제 방지)
        string escapedId = EscapeJsonString(npcId);
        string escapedName = EscapeJsonString(name);
        string escapedPersona = EscapeJsonString(persona);

        string jsonData = $"{{\"npc_id\":\"{escapedId}\",\"name\":\"{escapedName}\",\"persona\":\"{escapedPersona}\"}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest($"{serverURL}/npc/create", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"NPC 생성 실패: {request.error}";
                DebugLog(errorMsg);
                OnConnectionError?.Invoke(errorMsg);
            }
            else
            {
                DebugLog($"NPC '{name}' 생성 성공!");
                // 목록 새로고침
                GetNPCList();
            }
        }
    }

    /// <summary>
    /// 디버그 로그 출력
    /// </summary>
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ChatManager] {message}");
        }
    }

    /// <summary>
    /// JSON 문자열 이스케이프 처리
    /// </summary>
    private string EscapeJsonString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        return input.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
    }
}