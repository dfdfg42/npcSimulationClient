using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

[System.Serializable]
public class SimpleNPCStatus
{
    public string current_action;
    public string description;
    public string emoji;
    public string location;
    public string emotion;
}

[System.Serializable]
public class SimpleStatusResponse
{
    public SimpleNPCStatus status;
}

/// <summary>
/// 기존 NPCController와 함께 동작하는 자율 이동 컴포넌트
/// 대화는 기존 시스템을 사용하고, 자율 행동만 추가
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AutonomousMovement : MonoBehaviour
{
    [Header("자율 행동 설정")]
    public string npcId = "seoa";
    public bool enableAutonomousMovement = true;
    public float statusUpdateInterval = 30f;
    public float movementSpeed = 2f;

    [Header("위치 설정")]
    public Transform[] waypoints; // 이동 가능한 위치들
    public float arrivalDistance = 2f;

    [Header("UI 표시 (선택사항)")]
    public TextMeshProUGUI statusText; // 현재 행동 표시
    public GameObject statusUI; // 상태 UI 전체

    [Header("디버그")]
    public bool showDebugInfo = true;

    // 내부 변수
    private NavMeshAgent navAgent;
    private NPCController existingNPCController; // 기존 NPC 컨트롤러 참조
    private SimpleNPCStatus currentStatus;
    private Vector3 currentTarget;
    private bool isPlayerInteracting = false;
    private bool isMovingToTarget = false;

    // 위치 매핑 (서버 위치 → Unity 위치)
    private System.Collections.Generic.Dictionary<string, Vector3> locationMap;

    private void Start()
    {
        // 컴포넌트 초기화
        navAgent = GetComponent<NavMeshAgent>();
        existingNPCController = GetComponent<NPCController>();

        // NavMesh 설정
        navAgent.speed = movementSpeed;
        navAgent.stoppingDistance = arrivalDistance;

        // 위치 매핑 초기화
        InitializeLocationMap();

        // 기존 NPC 컨트롤러가 대화 중일 때 감지
        if (existingNPCController != null)
        {
            // 대화 상태 변화 감지를 위한 코루틴 시작
            StartCoroutine(MonitorInteractionState());
        }

        // 자율 행동 시작
        if (enableAutonomousMovement)
        {
            StartCoroutine(AutonomousMovementLoop());
        }

        Debug.Log($"[AutonomousMovement] {npcId} 자율 이동 시스템 시작");
    }

    private void InitializeLocationMap()
    {
        locationMap = new System.Collections.Generic.Dictionary<string, Vector3>();

        // waypoints 배열을 기반으로 위치 매핑
        if (waypoints != null && waypoints.Length >= 6)
        {
            locationMap["집:침실"] = waypoints[0].position;
            locationMap["집:부엌"] = waypoints[1].position;
            locationMap["도서관:열람실"] = waypoints[2].position;
            locationMap["카페:휴게실"] = waypoints[3].position;
            locationMap["대학교:강의실"] = waypoints[4].position;
            locationMap["대학교:중앙광장"] = waypoints[5].position;
        }

        // 기본 위치가 없으면 현재 위치 사용
        if (locationMap.Count == 0)
        {
            locationMap["기본위치"] = transform.position;
        }
    }

    private IEnumerator AutonomousMovementLoop()
    {
        while (enableAutonomousMovement)
        {
            // 플레이어와 상호작용 중이 아닐 때만 자율 이동
            if (!isPlayerInteracting)
            {
                yield return StartCoroutine(UpdateNPCStatusAndMove());
            }

            yield return new WaitForSeconds(statusUpdateInterval);
        }
    }

    private IEnumerator MonitorInteractionState()
    {
        while (true)
        {
            // 기존 NPC 컨트롤러의 대화 상태 확인
            bool wasTalking = isPlayerInteracting;
            isPlayerInteracting = IsNPCTalking();

            // 대화 상태가 변경되었을 때
            if (wasTalking != isPlayerInteracting)
            {
                if (isPlayerInteracting)
                {
                    OnStartTalking();
                }
                else
                {
                    OnStopTalking();
                }
            }

            yield return new WaitForSeconds(1f); // 1초마다 체크
        }
    }

    private bool IsNPCTalking()
    {
        // 기존 NPC 컨트롤러의 isTalking 상태 확인
        if (existingNPCController != null)
        {
            // NPCController의 private 필드에 접근하기 위해 reflection 사용하거나
            // NPCController를 수정해서 public 프로퍼티 추가
            // 여기서는 간단하게 ChatUI가 열려있는지로 판단
            ChatUI chatUI = FindObjectOfType<ChatUI>();
            return chatUI != null && chatUI.gameObject.activeInHierarchy &&
                   chatUI.transform.GetChild(0).gameObject.activeInHierarchy; // chatPanel 활성화 상태
        }
        return false;
    }

    private void OnStartTalking()
    {
        // 대화 시작 시 이동 중지
        if (navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = true;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[AutonomousMovement] {npcId} 대화 시작 - 자율 이동 일시 중지");
        }
    }

    private void OnStopTalking()
    {
        // 대화 종료 시 이동 재개
        if (navAgent.isActiveAndEnabled)
        {
            navAgent.isStopped = false;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[AutonomousMovement] {npcId} 대화 종료 - 자율 이동 재개");
        }

        // 서버에 상호작용 종료 알림
        StartCoroutine(NotifyInteractionEnd());
    }

    private IEnumerator UpdateNPCStatusAndMove()
    {
        string url = $"http://localhost:8000/npc/{npcId}/status";

        using (var request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<SimpleStatusResponse>(request.downloadHandler.text);
                    currentStatus = response.status;

                    // UI 업데이트
                    UpdateStatusUI();

                    // 새로운 위치로 이동
                    MoveToNewLocation();

                    if (showDebugInfo)
                    {
                        Debug.Log($"[AutonomousMovement] {npcId} 상태: {currentStatus.emoji} {currentStatus.current_action}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[AutonomousMovement] 상태 파싱 오류: {e.Message}");
                }
            }
            else
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"[AutonomousMovement] 서버 응답 없음: {request.error}");
                }
            }
        }
    }

    private void UpdateStatusUI()
    {
        if (currentStatus == null) return;

        // 상태 텍스트 업데이트
        if (statusText != null)
        {
            statusText.text = $"{currentStatus.emoji} {currentStatus.current_action}";
        }

        // 상태 UI 활성화/비활성화 (대화 중이 아닐 때만 표시)
        if (statusUI != null)
        {
            statusUI.SetActive(!isPlayerInteracting);
        }
    }

    private void MoveToNewLocation()
    {
        if (currentStatus == null || navAgent == null || !navAgent.isActiveAndEnabled)
            return;

        // 현재 위치와 다른 새로운 위치인지 확인
        Vector3 targetPosition = GetTargetPosition(currentStatus.location);

        if (Vector3.Distance(transform.position, targetPosition) > arrivalDistance)
        {
            navAgent.SetDestination(targetPosition);
            currentTarget = targetPosition;
            isMovingToTarget = true;

            if (showDebugInfo)
            {
                Debug.Log($"[AutonomousMovement] {npcId} 이동 시작: {currentStatus.location}");
            }
        }
    }

    private Vector3 GetTargetPosition(string locationName)
    {
        // 위치 매핑에서 찾기
        if (locationMap.ContainsKey(locationName))
        {
            return locationMap[locationName];
        }

        // 매핑에 없으면 랜덤 waypoint 선택
        if (waypoints != null && waypoints.Length > 0)
        {
            int randomIndex = Random.Range(0, waypoints.Length);
            return waypoints[randomIndex].position;
        }

        // 모든 방법이 실패하면 현재 위치 반환
        return transform.position;
    }

    private IEnumerator NotifyInteractionEnd()
    {
        string url = $"http://localhost:8000/npc/{npcId}/end_interaction";

        using (var request = UnityEngine.Networking.UnityWebRequest.Post(url, ""))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"[AutonomousMovement] 상호작용 종료 알림 실패: {request.error}");
                }
            }
        }
    }

    private void Update()
    {
        // 목적지 도착 체크
        if (isMovingToTarget && navAgent != null && navAgent.isActiveAndEnabled)
        {
            if (!navAgent.pathPending && navAgent.remainingDistance < arrivalDistance)
            {
                OnArriveAtDestination();
            }
        }
    }

    private void OnArriveAtDestination()
    {
        isMovingToTarget = false;

        if (showDebugInfo && currentStatus != null)
        {
            Debug.Log($"[AutonomousMovement] {npcId} 도착: {currentStatus.location}");
        }
    }

    // 수동으로 자율 이동 활성화/비활성화
    public void SetAutonomousMovement(bool enabled)
    {
        enableAutonomousMovement = enabled;

        if (!enabled && navAgent != null)
        {
            navAgent.isStopped = true;
        }

        Debug.Log($"[AutonomousMovement] {npcId} 자율 이동: {(enabled ? "활성화" : "비활성화")}");
    }

    // 현재 상태 정보 반환 (외부에서 참조용)
    public SimpleNPCStatus GetCurrentStatus()
    {
        return currentStatus;
    }

    // 현재 이동 중인지 확인
    public bool IsMoving()
    {
        return isMovingToTarget;
    }

    // 디버그 정보 표시
    private void OnDrawGizmosSelected()
    {
        // 목표 위치 표시
        if (currentTarget != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentTarget, 0.5f);
            Gizmos.DrawLine(transform.position, currentTarget);
        }

        // waypoints 표시
        if (waypoints != null)
        {
            Gizmos.color = Color.green;
            foreach (var point in waypoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 0.3f);
                }
            }
        }

        // 도착 거리 표시
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, arrivalDistance);
    }
}