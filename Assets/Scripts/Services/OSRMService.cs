using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Centralized service for handling OSRM API requests and route calculations
/// Provides a single point of configuration and shared functionality for all route-related scripts
/// </summary>
public class OSRMService : MonoBehaviour
{
    [Header("OSRM API Settings")]
    [SerializeField] private string osrmServerUrl = "https://router.project-osrm.org";
    [SerializeField] private float defaultRequestTimeout = 10f;
    [SerializeField] private int maxRetryAttempts = 2;
    
    [Header("Debug Settings")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool logRequestUrls = false;
    [SerializeField] private bool logResponseData = false;
    
    private static OSRMService instance;
    public static OSRMService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<OSRMService>();
                if (instance == null)
                {
                    GameObject go = new GameObject("OSRMService");
                    instance = go.AddComponent<OSRMService>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }
    
    private bool isRouteRequestPending = false;
    private int currentRequestId = 0;
    
    private MapTileGetter mapTileGetter;
    
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
            return;
        }
        
        if (mapTileGetter == null)
        {
            mapTileGetter = FindFirstObjectByType<MapTileGetter>();
        }
    }
    
    void Start()
    {
        if (mapTileGetter == null)
        {
            Debug.LogWarning("OSRMService: MapTileGetter not found! GPS coordinate conversion may not work properly.");
        }
    }
    
    /// <summary>
    /// Calculates a route between two GPS coordinates using OSRM
    /// </summary>
    /// <param name="startGPS">Starting GPS coordinates (lat, lon)</param>
    /// <param name="endGPS">Ending GPS coordinates (lat, lon)</param>
    /// <param name="onComplete">Callback with success status and route points in world coordinates</param>
    /// <param name="requestTimeout">Optional custom timeout (uses default if not specified)</param>
    public void CalculateRoute(Vector2 startGPS, Vector2 endGPS, System.Action<bool, List<Vector3>> onComplete, float requestTimeout = -1f)
    {
        if (isRouteRequestPending)
        {
            if (showDebugInfo)
            {
                Debug.Log("OSRMService: Route request already in progress, ignoring new request");
            }
            onComplete?.Invoke(false, null);
            return;
        }
        
        if (requestTimeout < 0f)
            requestTimeout = defaultRequestTimeout;
        
        isRouteRequestPending = true;
        int requestId = ++currentRequestId;
        
        if (showDebugInfo)
        {
            Debug.Log($"OSRMService: Starting route calculation #{requestId} from GPS({startGPS.x:F6}, {startGPS.y:F6}) to GPS({endGPS.x:F6}, {endGPS.y:F6})");
        }
        
        StartCoroutine(CalculateRouteCoroutine(startGPS, endGPS, (success, points) =>
        {
            if (requestId != currentRequestId)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"OSRMService: Ignoring outdated route response #{requestId} (current: #{currentRequestId})");
                }
                return;
            }
            
            isRouteRequestPending = false;
            
            if (showDebugInfo)
            {
                Debug.Log($"OSRMService: Route calculation #{requestId} completed with {(success ? "success" : "failure")}");
            }
            
            onComplete?.Invoke(success, points);
        }, requestTimeout, requestId));
    }
    
    /// <summary>
    /// Calculates a route between two GPS coordinates using OSRM (Asynchronous)
    /// </summary>
    /// <param name="startGPS">Starting GPS coordinates (lat, lon)</param>
    /// <param name="endGPS">Ending GPS coordinates (lat, lon)</param>
    /// <param name="requestTimeout">Optional custom timeout (uses default if not specified)</param>
    /// <returns>Task with result containing success status and route points in world coordinates</returns>
    public async Task<(bool success, List<Vector3> routePoints)> CalculateRouteAsync(Vector2 startGPS, Vector2 endGPS, float requestTimeout = -1f)
    {
        if (isRouteRequestPending)
        {
            if (showDebugInfo)
            {
                Debug.Log("OSRMService: Route request already in progress, ignoring new async request");
            }
            return (false, null);
        }
        
        if (requestTimeout < 0f)
            requestTimeout = defaultRequestTimeout;

        isRouteRequestPending = true;
        int requestId = ++currentRequestId;
        
        if (showDebugInfo)
        {
            Debug.Log($"OSRMService: Starting async route calculation #{requestId} from GPS({startGPS.x:F6}, {startGPS.y:F6}) to GPS({endGPS.x:F6}, {endGPS.y:F6})");
        }

        var tcs = new TaskCompletionSource<(bool, List<Vector3>)>();
        
        StartCoroutine(CalculateRouteCoroutine(startGPS, endGPS, (success, points) =>
        {
            if (requestId != currentRequestId)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"OSRMService: Ignoring outdated async route response #{requestId} (current: #{currentRequestId})");
                }
                tcs.SetResult((false, null));
                return;
            }
            
            isRouteRequestPending = false;
            
            if (showDebugInfo)
            {
                Debug.Log($"OSRMService: Async route calculation #{requestId} completed with {(success ? "success" : "failure")}");
            }
            
            tcs.SetResult((success, points));
        }, requestTimeout, requestId));
        
        return await tcs.Task;
    }
    
    /// <summary>
    /// Calculates a route between two world positions using OSRM (Asynchronous)
    /// </summary>
    /// <param name="startWorldPos">Starting world position</param>
    /// <param name="endWorldPos">Ending world position</param>
    /// <param name="requestTimeout">Optional custom timeout (uses default if not specified)</param>
    /// <returns>Task with result containing success status and route points in world coordinates</returns>
    public async Task<(bool success, List<Vector3> routePoints)> CalculateRouteFromWorldPositionsAsync(Vector3 startWorldPos, Vector3 endWorldPos, float requestTimeout = -1f)
    {
        if (mapTileGetter == null)
        {
            Debug.LogError("OSRMService: MapTileGetter not found for world position conversion!");
            return (false, null);
        }
        
        Vector2 startGPS = mapTileGetter.WorldToGPSCoordinates(startWorldPos);
        Vector2 endGPS = mapTileGetter.WorldToGPSCoordinates(endWorldPos);
        
        return await CalculateRouteAsync(startGPS, endGPS, requestTimeout);
    }
    
    /// <summary>
    /// Internal coroutine for route calculation
    /// </summary>
    IEnumerator CalculateRouteCoroutine(Vector2 startGPS, Vector2 endGPS, System.Action<bool, List<Vector3>> onComplete, float requestTimeout, int requestId)
    {
        string coordinates = $"{startGPS.y:F6},{startGPS.x:F6};{endGPS.y:F6},{endGPS.x:F6}";
        string url = $"{osrmServerUrl}/route/v1/driving/{coordinates}?overview=full&geometries=geojson";
        
        if (logRequestUrls && showDebugInfo)
        {
            Debug.Log($"OSRMService: Request #{requestId} URL: {url}");
        }
        
        int attempts = 0;
        while (attempts < maxRetryAttempts)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = (int)requestTimeout;
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;
                    
                    if (logResponseData && showDebugInfo)
                    {
                        Debug.Log($"OSRMService: Response: {jsonResponse}");
                    }
                    
                    // Process successful response
                    yield return StartCoroutine(ProcessOSRMResponse(jsonResponse, onComplete, requestId));
                    yield break; // Exit successfully
                }
                else
                {
                    attempts++;
                    if (showDebugInfo)
                    {
                        Debug.LogWarning($"OSRMService: Request failed (attempt {attempts}/{maxRetryAttempts}): {request.error}");
                    }
                    
                    if (attempts < maxRetryAttempts)
                    {
                        yield return new WaitForSeconds(1f);
                    }
                }
            }
        }
        
        if (showDebugInfo)
        {
            Debug.LogError($"OSRMService: Request #{requestId} failed after {maxRetryAttempts} attempts");
        }
        
        if (requestId == currentRequestId)
        {
            isRouteRequestPending = false;
        }
        
        onComplete?.Invoke(false, null);
    }
    
    /// <summary>
    /// Processes OSRM API response and converts to world coordinates
    /// </summary>
    IEnumerator ProcessOSRMResponse(string jsonResponse, System.Action<bool, List<Vector3>> onComplete, int requestId)
    {
        try
        {
            if (requestId != currentRequestId)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"OSRMService: Ignoring outdated response for request #{requestId} (current: #{currentRequestId})");
                }
                yield break;
            }
            
            if (!jsonResponse.Contains("\"code\":\"Ok\""))
            {
                Debug.LogError($"OSRMService: Request #{requestId} - OSRM API returned non-OK status");
                isRouteRequestPending = false;
                onComplete?.Invoke(false, null);
                yield break;
            }
            
            List<Vector2> gpsCoordinates = ExtractCoordinatesFromJson(jsonResponse);
            
            if (gpsCoordinates == null || gpsCoordinates.Count == 0)
            {
                Debug.LogError($"OSRMService: Request #{requestId} - Failed to extract coordinates from response");
                isRouteRequestPending = false;
                onComplete?.Invoke(false, null);
                yield break;
            }
            
            List<Vector3> worldPoints = new List<Vector3>();
            
            if (mapTileGetter != null)
            {
                foreach (Vector2 gpsCoord in gpsCoordinates)
                {
                    Vector3 worldPos = mapTileGetter.ConvertGPSToWorldPosition(gpsCoord.x, gpsCoord.y);
                    worldPoints.Add(worldPos);
                }
            }
            else
            {
                Debug.LogError($"OSRMService: Request #{requestId} - MapTileGetter not available for coordinate conversion");
                isRouteRequestPending = false;
                onComplete?.Invoke(false, null);
                yield break;
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"OSRMService: Request #{requestId} - Successfully calculated route with {worldPoints.Count} points");
            }
            
            isRouteRequestPending = false;
            onComplete?.Invoke(true, worldPoints);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"OSRMService: Request #{requestId} - Error processing OSRM response: {e.Message}");
            
            if (requestId == currentRequestId)
            {
                isRouteRequestPending = false;
            }
            
            onComplete?.Invoke(false, null);
        }
    }
    
    /// <summary>
    /// Extracts coordinates from OSRM JSON response
    /// </summary>
    List<Vector2> ExtractCoordinatesFromJson(string jsonResponse)
    {
        List<Vector2> coordinates = new List<Vector2>();
        
        try
        {
            string searchPattern = "\"coordinates\":";
            int startIndex = jsonResponse.IndexOf(searchPattern);
            if (startIndex == -1) return null;
            
            startIndex += searchPattern.Length;
            
            int arrayStart = jsonResponse.IndexOf('[', startIndex);
            if (arrayStart == -1) return null;
            
            int bracketCount = 0;
            int arrayEnd = arrayStart;
            for (int i = arrayStart; i < jsonResponse.Length; i++)
            {
                if (jsonResponse[i] == '[') bracketCount++;
                else if (jsonResponse[i] == ']') bracketCount--;
                
                if (bracketCount == 0)
                {
                    arrayEnd = i;
                    break;
                }
            }
            
            string coordsString = jsonResponse.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            
            coordsString = coordsString.Replace(" ", "");
            string[] coordPairs = coordsString.Split(new string[] { "],[" }, System.StringSplitOptions.RemoveEmptyEntries);
            
            foreach (string pair in coordPairs)
            {
                string cleanPair = pair.Replace("[", "").Replace("]", "");
                string[] values = cleanPair.Split(',');
                
                if (values.Length >= 2)
                {
                    if (float.TryParse(values[0], out float longitude) && 
                        float.TryParse(values[1], out float latitude))
                    {
                        coordinates.Add(new Vector2(latitude, longitude));
                    }
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"OSRMService: Extracted {coordinates.Count} coordinate pairs");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"OSRMService: Error parsing coordinates from JSON: {e.Message}");
            return null;
        }
        
        return coordinates;
    }
    
    /// <summary>
    /// Sets the OSRM server URL
    /// </summary>
    public void SetOSRMServerUrl(string url)
    {
        osrmServerUrl = url;
        if (showDebugInfo)
        {
            Debug.Log($"OSRMService: Server URL set to: {url}");
        }
    }
    
    /// <summary>
    /// Gets the current OSRM server URL
    /// </summary>
    public string GetOSRMServerUrl()
    {
        return osrmServerUrl;
    }
    
    /// <summary>
    /// Sets the default request timeout
    /// </summary>
    public void SetDefaultTimeout(float timeout)
    {
        defaultRequestTimeout = Mathf.Max(1f, timeout);
        if (showDebugInfo)
        {
            Debug.Log($"OSRMService: Default timeout set to: {defaultRequestTimeout}s");
        }
    }
    
    /// <summary>
    /// Gets the default request timeout
    /// </summary>
    public float GetDefaultTimeout()
    {
        return defaultRequestTimeout;
    }
    
    /// <summary>
    /// Sets the maximum retry attempts
    /// </summary>
    public void SetMaxRetryAttempts(int attempts)
    {
        maxRetryAttempts = Mathf.Max(1, attempts);
        if (showDebugInfo)
        {
            Debug.Log($"OSRMService: Max retry attempts set to: {maxRetryAttempts}");
        }
    }
    
    /// <summary>
    /// Gets the maximum retry attempts
    /// </summary>
    public int GetMaxRetryAttempts()
    {
        return maxRetryAttempts;
    }
    
    /// <summary>
    /// Sets the MapTileGetter reference manually if needed
    /// </summary>
    public void SetMapTileGetter(MapTileGetter mapTileGetter)
    {
        this.mapTileGetter = mapTileGetter;
        if (showDebugInfo)
        {
            Debug.Log("OSRMService: MapTileGetter reference set manually");
        }
    }
    
    /// <summary>
    /// Checks if the service is properly configured
    /// </summary>
    public bool IsConfigured()
    {
        return !string.IsNullOrEmpty(osrmServerUrl) && mapTileGetter != null;
    }
    
    /// <summary>
    /// Checks if a route request is currently pending
    /// </summary>
    /// <returns>True if a route calculation is in progress</returns>
    public bool IsRouteRequestPending()
    {
        return isRouteRequestPending;
    }
    
    /// <summary>
    /// Gets the current request ID (useful for debugging)
    /// </summary>
    /// <returns>Current request ID, or 0 if no request has been made</returns>
    public int GetCurrentRequestId()
    {
        return currentRequestId;
    }
    
    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
