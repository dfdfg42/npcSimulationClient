// LocationManager.cs
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// 서버로 보낼 데이터 형식
[System.Serializable]
public class LocationUpdateRequest
{
    public List<string> locations;
}

public class LocationManager : MonoBehaviour
{
    public static LocationManager Instance;

    private string serverURL = "http://localhost:8000"; // ChatManager와 동일하게 설정

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 씬 로딩이 완료된 후 잠시 뒤에 실행하여 모든 NPC가 로드되도록 함
        StartCoroutine(SendLocationsAfterDelay(2.0f));
    }

    private IEnumerator SendLocationsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SendAvailableLocations();
    }

    /// <summary>
    /// 씬에 있는 모든 NPC의 이동 가능 위치를 수집하여 서버로 전송합니다.
    /// </summary>
    public void SendAvailableLocations()
    {
        // 씬에 있는 모든 AutonomousMovement 컴포넌트를 찾습니다.
        AutonomousMovement[] npcMovements = FindObjectsOfType<AutonomousMovement>();

        // 중복을 제거한 위치 이름 목록을 만듭니다.
        HashSet<string> locationNames = new HashSet<string>();

        foreach (var movement in npcMovements)
        {
            // AutonomousMovement 스크립트에서 위치 이름을 가져옵니다.
            // 이 부분은 해당 스크립트의 구현에 따라 달라질 수 있습니다.
            // 여기서는 GetLocationNames() 라는 함수가 있다고 가정합니다.
            List<string> names = movement.GetLocationNames();
            foreach (var name in names)
            {
                locationNames.Add(name);
            }
        }

        if (locationNames.Count > 0)
        {
            Debug.Log($"[LocationManager] {locationNames.Count}개의 고유한 위치를 서버로 전송합니다: " + string.Join(", ", locationNames));
            StartCoroutine(UpdateLocationsOnServer(locationNames.ToList()));
        }
        else
        {
            Debug.LogWarning("[LocationManager] 씬에서 수집할 수 있는 위치 정보가 없습니다.");
        }
    }

    private IEnumerator UpdateLocationsOnServer(List<string> locations)
    {
        string url = $"{serverURL}/system/locations/update";

        LocationUpdateRequest requestData = new LocationUpdateRequest { locations = locations };
        string jsonData = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[LocationManager] 서버에 위치 정보 전송 성공!");
            }
            else
            {
                Debug.LogError($"[LocationManager] 위치 정보 전송 실패: {request.error}");
            }
        }
    }
}