using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public class TileService : MonoBehaviour
{
    [System.Serializable]
    public class TileProvider
    {
        public string name;
        public string urlTemplate;
        public int maxZoom = 19;
        public string attribution;
    }

    [Header("Tile Providers")]
    [SerializeField] private TileProvider[] providers = new TileProvider[]
    {
        new TileProvider { name = "OpenStreetMap", urlTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png", maxZoom = 19 },
        new TileProvider { name = "OpenTopoMap", urlTemplate = "https://tile.opentopomap.org/{z}/{x}/{y}.png", maxZoom = 17 }
    };

    [Header("Settings")]
    [SerializeField] private int currentProviderIndex = 0;
    [SerializeField] private float requestTimeout = 15f;
    [SerializeField] private int maxRetryAttempts = 3;
    [SerializeField] private float retryDelay = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private bool logDownloadUrls = false;

    private static TileService instance;
    public static TileService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<TileService>();
                if (instance == null)
                {
                    GameObject go = new GameObject("TileService");
                    instance = go.AddComponent<TileService>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    private Dictionary<string, bool> pendingTiles = new Dictionary<string, bool>();

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

    public void DownloadTile(int zoom, int x, int y, System.Action<bool, Texture2D> onComplete)
    {
        string tileKey = GetTileKey(zoom, x, y);
        
        if (pendingTiles.ContainsKey(tileKey))
        {
            if (showDebugInfo)
                Debug.Log($"TileService: Tile {tileKey} already being downloaded");
            onComplete?.Invoke(false, null);
            return;
        }

        pendingTiles[tileKey] = true;
        StartCoroutine(DownloadTileCoroutine(zoom, x, y, onComplete));
    }

    public async Task<(bool success, Texture2D texture)> DownloadTileAsync(int zoom, int x, int y)
    {
        string tileKey = GetTileKey(zoom, x, y);
        
        if (pendingTiles.ContainsKey(tileKey))
        {
            if (showDebugInfo)
                Debug.Log($"TileService: Tile {tileKey} already being downloaded");
            return (false, null);
        }

        pendingTiles[tileKey] = true;

        var taskCompletionSource = new TaskCompletionSource<(bool, Texture2D)>();
        
        StartCoroutine(DownloadTileCoroutine(zoom, x, y, (success, texture) =>
        {
            taskCompletionSource.SetResult((success, texture));
        }));

        return await taskCompletionSource.Task;
    }

    IEnumerator DownloadTileCoroutine(int zoom, int x, int y, System.Action<bool, Texture2D> onComplete)
    {
        string tileKey = GetTileKey(zoom, x, y);
        string url = BuildTileUrl(zoom, x, y);
        
        if (logDownloadUrls && showDebugInfo)
            Debug.Log($"TileService: Downloading {url}");

        int attempts = 0;
        
        while (attempts < maxRetryAttempts)
        {
            attempts++;
            
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = (int)requestTimeout;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(request);
                    texture.filterMode = FilterMode.Bilinear;
                    
                    if (showDebugInfo)
                        Debug.Log($"TileService: Successfully downloaded tile {tileKey}");
                    
                    pendingTiles.Remove(tileKey);
                    onComplete?.Invoke(true, texture);
                    yield break;
                }
                else
                {
                    if (showDebugInfo)
                        Debug.LogError($"TileService: Download failed ({attempts}/{maxRetryAttempts}) - {request.error}");
                    
                    if (attempts < maxRetryAttempts)
                        yield return new WaitForSeconds(retryDelay);
                }
            }
        }

        if (showDebugInfo)
            Debug.LogError($"TileService: All download attempts failed for tile {tileKey}");
        
        pendingTiles.Remove(tileKey);
        onComplete?.Invoke(false, null);
    }

    string BuildTileUrl(int zoom, int x, int y)
    {
        if (currentProviderIndex < 0 || currentProviderIndex >= providers.Length)
            currentProviderIndex = 0;

        string template = providers[currentProviderIndex].urlTemplate;
        return template.Replace("{z}", zoom.ToString())
                      .Replace("{x}", x.ToString())
                      .Replace("{y}", y.ToString());
    }

    string GetTileKey(int zoom, int x, int y) => $"{zoom}_{x}_{y}";

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
