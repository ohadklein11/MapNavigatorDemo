using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class GeolocationService : MonoBehaviour
{
    [System.Serializable]
    public class GeoInfo
    {
        public string ip;
        public string city;
        public string region;
        public string country;
        public float latitude;
        public float longitude;
    }

    [Header("API Settings")]
    [SerializeField] private string primaryUrl = "https://ipapi.co/json/";
    [SerializeField] private string fallbackUrl = "http://ip-api.com/json/";
    [SerializeField] private float requestTimeout = 10f;
    [SerializeField] private int maxRetryAttempts = 2;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    private static GeolocationService instance;
    public static GeolocationService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<GeolocationService>();
                if (instance == null)
                {
                    GameObject go = new GameObject("GeolocationService");
                    instance = go.AddComponent<GeolocationService>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    private bool isRequestPending = false;

    void Awake()
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

    public void GetLocation(System.Action<bool, GeoInfo> onComplete)
    {
        if (isRequestPending)
        {
            if (showDebugInfo)
                Debug.Log("GeolocationService: Request already in progress");
            onComplete?.Invoke(false, null);
            return;
        }

        isRequestPending = true;
        StartCoroutine(GetLocationCoroutine(onComplete));
    }

    IEnumerator GetLocationCoroutine(System.Action<bool, GeoInfo> onComplete)
    {
        GeoInfo result = null;
        bool success = false;

        // Try primary URL first
        yield return StartCoroutine(TryGetLocation(primaryUrl, (s, data) => {
            success = s;
            result = data;
        }));

        // Try fallback if primary failed
        if (!success)
        {
            if (showDebugInfo)
                Debug.Log("GeolocationService: Primary failed, trying fallback");
            
            yield return StartCoroutine(TryGetLocation(fallbackUrl, (s, data) => {
                success = s;
                result = data;
            }));
        }

        isRequestPending = false;
        
        if (showDebugInfo)
        {
            if (success)
                Debug.Log($"GeolocationService: Success - Lat {result.latitude}, Lon {result.longitude}");
            else
                Debug.LogError("GeolocationService: All attempts failed");
        }

        onComplete?.Invoke(success, result);
    }

    IEnumerator TryGetLocation(string url, System.Action<bool, GeoInfo> onResult)
    {
        int attempts = 0;
        
        while (attempts < maxRetryAttempts)
        {
            attempts++;
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = (int)requestTimeout;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        GeoInfo geo = JsonUtility.FromJson<GeoInfo>(request.downloadHandler.text);
                        onResult?.Invoke(true, geo);
                        yield break;
                    }
                    catch (System.Exception e)
                    {
                        if (showDebugInfo)
                            Debug.LogError($"GeolocationService: JSON parse error - {e.Message}");
                    }
                }
                else
                {
                    if (showDebugInfo)
                        Debug.LogError($"GeolocationService: Request failed ({attempts}/{maxRetryAttempts}) - {request.error}");
                }

                if (attempts < maxRetryAttempts)
                    yield return new WaitForSeconds(1f);
            }
        }

        onResult?.Invoke(false, null);
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
