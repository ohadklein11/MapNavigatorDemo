using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Fetches and displays map tiles based on estimated user location.
/// Uses centralized services for geolocation and tile downloading.
/// Supports dynamic loading of surrounding tiles as the camera moves.
/// </summary>
public class MapTileGetter : MonoBehaviour
{

    [Range(1, 19)]
    public int zoom = 13;

    private int tilesAround = 5;
    private float tileSize = 1f; // Unity units per tile
    private float pixelsPerUnit = 256f;

    [Header("Tile Organization")]
    [SerializeField, Tooltip("Parent Transform for all map tiles. If left empty, will create 'Map Tiles' object automatically.")]
    private Transform tilesParent;

    private Dictionary<string, GameObject> loadedTiles = new Dictionary<string, GameObject>();
    private HashSet<string> tilesBeingDownloaded = new HashSet<string>();
    private Camera mainCamera;
    private bool dynamicLoadingEnabled = false;

    // GPS coordinate system
    private double initialLat, initialLon;
    private int initialCenterTileX, initialCenterTileY;

    [Header("Player Position")]
    [SerializeField, Tooltip("The player position object to position at the correct GPS coordinates")]
    private Transform myPosObject;

    [Header("Dynamic Loading Settings")]
    public float visibilityCheckInterval = 0.5f; // How often to check for visible tiles
    public float loadingDistance = 2f; // Distance from camera to start loading tiles

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindFirstObjectByType<Camera>();

        EnsureTilesParent();

        StartCoroutine(GetEstimatedLocation());
    }

    void EnsureTilesParent()
    {
        if (tilesParent == null)
        {
            GameObject parentGO = new GameObject("Map Tiles");
            tilesParent = parentGO.transform;
            Debug.Log("Created 'Map Tiles' parent object automatically");
        }
    }

    IEnumerator GetEstimatedLocation()
    {
        GeolocationService.Instance.GetLocation((success, geoInfo) =>
        {
            if (!success || geoInfo == null)
            {
                Debug.LogError("Failed to get location from GeolocationService");
                return;
            }

            Debug.Log($"Estimated location: Lat {geoInfo.latitude}, Lon {geoInfo.longitude}");

            // Calculate total tiles that will be downloaded
            int gridSize = (tilesAround * 2 + 1);
            int totalTiles = gridSize * gridSize;

            if (LoadingScreen.Instance != null)
            {
                LoadingScreen.Instance.StartLoading(totalTiles, "Location found! Downloading map tiles...");
            }

            StartCoroutine(DownloadAndShowMap(geoInfo.latitude, geoInfo.longitude, zoom, tilesAround));
        });

        yield return null;
    }

    /// <summary>
    /// Enables dynamic loading of surrounding tiles as the camera moves.
    /// </summary>
    public void EnableDynamicLoading()
    {
        dynamicLoadingEnabled = true;
        StartCoroutine(VisibilityCheckCoroutine());
    }

    /// <summary>
    /// Disables dynamic loading of tiles.
    /// </summary>
    public void DisableDynamicLoading()
    {
        dynamicLoadingEnabled = false;
    }

    /// <summary>
    /// Clears all loaded tiles from the scene and memory.
    /// </summary>
    public void ClearLoadedTiles()
    {
        foreach (var tile in loadedTiles.Values)
        {
            if (tile != null)
                DestroyImmediate(tile);
        }
        loadedTiles.Clear();
        tilesBeingDownloaded.Clear();

        Debug.Log($"Cleared {loadedTiles.Count} loaded tiles");
    }

    void OnDisable()
    {
        DisableDynamicLoading();
    }

    /// <summary>
    /// Checks the visibility of tiles and loads any missing ones.
    /// </summary>
    IEnumerator VisibilityCheckCoroutine()
    {
        while (dynamicLoadingEnabled)
        {
            yield return new WaitForSeconds(visibilityCheckInterval);
            _ = CheckVisibleTilesAndLoadMissingAsync();
        }
    }

    /// <summary>
    /// Checks which tiles are visible in the camera view and ensures surrounding tiles are loaded.
    /// </summary>
    async Task CheckVisibleTilesAndLoadMissingAsync()
    {
        if (mainCamera == null) return;

        // Get camera bounds in world space
        float camHeight = mainCamera.orthographicSize * 2;
        float camWidth = camHeight * mainCamera.aspect;
        Vector3 camPos = mainCamera.transform.position;

        // Expand bounds by loading distance
        float expandedWidth = camWidth + loadingDistance * 2;
        float expandedHeight = camHeight + loadingDistance * 2;

        // Calculate tile range that should be visible
        int minTileX = Mathf.FloorToInt((camPos.x - expandedWidth / 2) / tileSize);
        int maxTileX = Mathf.CeilToInt((camPos.x + expandedWidth / 2) / tileSize);
        int minTileY = Mathf.FloorToInt((-camPos.y - expandedHeight / 2) / tileSize);
        int maxTileY = Mathf.CeilToInt((-camPos.y + expandedHeight / 2) / tileSize);

        int tilesChecked = 0;
        int surroundingTilesLoaded = 0;

        // Check each visible tile and ensure it has surrounding tiles
        for (int x = minTileX; x <= maxTileX; x++)
        {
            for (int y = minTileY; y <= maxTileY; y++)
            {
                string tileKey = GetTileKey(x, y);
                tilesChecked++;

                // If this tile exists, check for missing surrounding tiles
                if (loadedTiles.ContainsKey(tileKey))
                {
                    int loaded = await CheckAndLoadSurroundingTilesAsync(x, y);
                    surroundingTilesLoaded += loaded;
                }
            }
        }

        if (surroundingTilesLoaded > 0)
        {
            Debug.Log($"Dynamic loading: Checked {tilesChecked} tiles, started loading {surroundingTilesLoaded} surrounding tiles");
        }
    }

    /// <summary>
    /// Checks the 8 surrounding tiles of the given tile coordinates and starts downloading any that are missing.
    /// </summary>
    Task<int> CheckAndLoadSurroundingTilesAsync(int centerX, int centerY)
    {
        int tilesStartedLoading = 0;

        // Check the 8 surrounding tiles
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue; // Skip center tile

                int tileX = centerX + dx;
                int tileY = centerY + dy;
                string tileKey = GetTileKey(tileX, tileY);

                // If tile doesn't exist and isn't being downloaded, start downloading
                if (!loadedTiles.ContainsKey(tileKey) && !tilesBeingDownloaded.Contains(tileKey))
                {
                    Vector3 worldPos = TileToWorldPosition(tileX, tileY);
                    // Convert local coordinates to actual OpenStreetMap tile coordinates
                    int realTileX = initialCenterTileX + tileX;
                    int realTileY = initialCenterTileY + tileY;
                    Debug.Log($"Loading surrounding tile: local({tileX},{tileY}) -> real({realTileX},{realTileY}) at world pos {worldPos}");
                    _ = DownloadTileDynamicAsync(zoom, realTileX, realTileY, worldPos, tileX, tileY);
                    tilesStartedLoading++;
                }
            }
        }

        return Task.FromResult(tilesStartedLoading);
    }

    string GetTileKey(int x, int y)
    {
        return $"{x}_{y}";
    }

    Vector3 TileToWorldPosition(int tileX, int tileY)
    {
        return new Vector3(tileX * tileSize, -tileY * tileSize, 0);
    }

    /// <summary>
    /// Converts world position to GPS coordinates
    /// </summary>
    void WorldToGPS(Vector3 worldPos, out double lat, out double lon)
    {
        // Convert world position to GPS coordinates based on initial position
        // This needs to account for the 0.5 pivot offset used in GPSToWorldPosition

        // Convert world coordinates back to tile offsets
        double offsetX = worldPos.x / tileSize;
        double offsetY = -worldPos.y / tileSize;

        // Add back the 0.5 pivot offset that was subtracted in GPSToWorldPosition
        double adjustedOffsetX = offsetX + 0.5;
        double adjustedOffsetY = offsetY + 0.5;

        // Convert back to exact tile coordinates
        double exactTileX = initialCenterTileX + adjustedOffsetX;
        double exactTileY = initialCenterTileY + adjustedOffsetY;

        // Convert tile coordinates back to GPS
        lon = (exactTileX / (1 << zoom)) * 360.0 - 180.0;

        // Convert Y tile coordinate back to latitude
        double n = System.Math.PI - 2.0 * System.Math.PI * exactTileY / (1 << zoom);
        lat = (180.0 / System.Math.PI) * System.Math.Atan(0.5 * (System.Math.Exp(n) - System.Math.Exp(-n)));
    }

    /// <summary>
    /// Converts GPS coordinates to precise world position within a tile
    /// Takes into account the tile's 0.5 pivot offset
    /// </summary>
    Vector3 GPSToWorldPosition(double lat, double lon)
    {
        // Convert GPS to exact tile coordinates (with fractional part)
        double exactTileX = (lon + 180.0) / 360.0 * (1 << zoom);
        double latRad = lat * Mathf.Deg2Rad;
        double exactTileY = (1.0 - System.Math.Log(System.Math.Tan(latRad) + 1.0 / System.Math.Cos(latRad)) / System.Math.PI) / 2.0 * (1 << zoom);

        // Calculate offset from the initial center tile
        double offsetX = exactTileX - initialCenterTileX;
        double offsetY = exactTileY - initialCenterTileY;

        // Account for the 0.5 pivot offset of the tiles
        // Tiles are centered on their transform position, so we need to adjust:
        // x - 0.5 and y - 0.5 to position correctly within the tile
        double adjustedOffsetX = offsetX - 0.5;
        double adjustedOffsetY = offsetY - 0.5;

        // Convert to world coordinates
        float worldX = (float)adjustedOffsetX * tileSize;
        float worldY = -(float)adjustedOffsetY * tileSize;

        return new Vector3(worldX, worldY, 0);
    }

    /// <summary>
    /// Positions the MyPos object at the correct GPS coordinates
    /// Call this after the initial GPS location is determined
    /// </summary>
    public void PositionMyPosAtGPS(double lat, double lon)
    {
        if (myPosObject == null)
        {
            Debug.LogWarning("MyPos object not assigned! Please assign it in the inspector.");
            return;
        }

        Vector3 worldPos = GPSToWorldPosition(lat, lon);
        myPosObject.position = worldPos;

        Debug.Log($"Positioned MyPos at GPS ({lat}, {lon}) -> World position {worldPos}");
    }

    /// <summary>
    /// Centers the camera on the MyPos object's position
    /// </summary>
    public void CenterCameraOnMyPos()
    {
        if (mainCamera == null)
        {
            Debug.LogWarning("Main camera not found!");
            return;
        }

        if (myPosObject == null)
        {
            Debug.LogWarning("MyPos object not assigned! Cannot center camera.");
            return;
        }

        // Position camera at MyPos location, keeping the camera's Z position
        Vector3 cameraPos = mainCamera.transform.position;
        cameraPos.x = myPosObject.position.x;
        cameraPos.y = myPosObject.position.y;
        mainCamera.transform.position = cameraPos;

        Debug.Log($"Centered camera on MyPos at position {myPosObject.position}");
    }

    async Task DownloadTileDynamicAsync(int zoom, int x, int y, Vector3 position, int localX, int localY)
    {
        string tileKey = GetTileKey(localX, localY);
        tilesBeingDownloaded.Add(tileKey);

        var result = await TileService.Instance.DownloadTileAsync(zoom, x, y);
        
        tilesBeingDownloaded.Remove(tileKey);

        if (!result.success || result.texture == null)
        {
            Debug.LogError($"Dynamic tile download failed for tile {x},{y}");
        }
        else
        {
            Debug.Log($"Dynamic tile {x},{y} downloaded successfully.");
            
            GameObject tileGO = new GameObject($"tile_{x}_{y}");
            tileGO.transform.position = position;

            if (tilesParent != null)
                tileGO.transform.SetParent(tilesParent);

            var sr = tileGO.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(result.texture,
                new Rect(0, 0, result.texture.width, result.texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
            tileGO.transform.localScale = Vector3.one * tileSize;
            loadedTiles[tileKey] = tileGO;
        }
    }

    IEnumerator DownloadAndShowMap(double lat, double lon, int zoom, int radius)
    {
        initialLat = lat;
        initialLon = lon;

        // Convert GPS -> tile X/Y
        initialCenterTileX = (int)((lon + 180.0) / 360.0 * (1 << zoom));
        float latRad = (float)(lat * Mathf.Deg2Rad);
        initialCenterTileY = (int)((1.0 - Mathf.Log(Mathf.Tan(latRad) + 1.0f / Mathf.Cos(latRad)) / Mathf.PI) / 2.0 * (1 << zoom));

        PositionMyPosAtGPS(lat, lon);
        CenterCameraOnMyPos();

        // start downloading tiles
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int tileX = initialCenterTileX + dx;
                int tileY = initialCenterTileY + dy;

                Vector3 pos = new Vector3(dx * tileSize, -dy * tileSize, 0);
                StartCoroutine(DownloadTile(zoom, tileX, tileY, pos, dx, dy));
            }
        }

        yield return new WaitForSeconds(2f);
        EnableDynamicLoading();

        yield return null;
    }

    IEnumerator DownloadTile(int zoom, int x, int y, Vector3 position, int localX, int localY)
    {
        string tileKey = GetTileKey(localX, localY);
        tilesBeingDownloaded.Add(tileKey);

        bool completed = false;
        bool success = false;
        Texture2D texture = null;

        TileService.Instance.DownloadTile(zoom, x, y, (downloadSuccess, downloadTexture) =>
        {
            success = downloadSuccess;
            texture = downloadTexture;
            completed = true;
        });

        // Wait for completion
        yield return new WaitUntil(() => completed);

        tilesBeingDownloaded.Remove(tileKey);

        if (!success || texture == null)
        {
            Debug.LogError("Tile download failed");

            if (LoadingScreen.Instance != null)
            {
                LoadingScreen.Instance.IncrementProgress($"Failed to download tile {x},{y}");
            }
        }
        else
        {
            Debug.Log($"Tile {x},{y} downloaded successfully.");

            GameObject tileGO = new GameObject($"tile_{x}_{y}");
            tileGO.transform.position = position;

            if (tilesParent != null)
                tileGO.transform.SetParent(tilesParent);

            var sr = tileGO.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
            tileGO.transform.localScale = Vector3.one * tileSize;
            loadedTiles[tileKey] = tileGO;

            if (LoadingScreen.Instance != null)
            {
                LoadingScreen.Instance.IncrementProgress($"Downloaded tile {x},{y}");
            }
        }
    }

    public float GetTileSize()
    {
        return tileSize;
    }

    public async Task CheckAndLoadSurroundingTilesPublicAsync(int centerX, int centerY)
    {
        Debug.Log($"Public async check for surrounding tiles of {centerX},{centerY}");
        int loaded = await CheckAndLoadSurroundingTilesAsync(centerX, centerY);
        Debug.Log($"Started loading {loaded} surrounding tiles for {centerX},{centerY}");
    }

    /// <summary>
    /// Converts world position to GPS coordinates
    /// Public version of the private WorldToGPS method
    /// </summary>
    public Vector2 WorldToGPSCoordinates(Vector3 worldPos)
    {
        double lat, lon;
        WorldToGPS(worldPos, out lat, out lon);
        return new Vector2((float)lat, (float)lon);
    }

    /// <summary>
    /// Converts GPS coordinates to world position (public version)
    /// </summary>
    public Vector3 ConvertGPSToWorldPosition(double lat, double lon)
    {
        return GPSToWorldPosition(lat, lon);
    }

    /// <summary>
    /// Converts GPS coordinates to world position (public version with Vector2 input)
    /// </summary>
    public Vector3 ConvertGPSToWorldPosition(Vector2 gpsCoords)
    {
        return GPSToWorldPosition(gpsCoords.x, gpsCoords.y);
    }

    /// <summary>
    /// Public property to access the MyPos object transform
    /// </summary>
    public Transform MyPosObject
    {
        get { return myPosObject; }
        set { myPosObject = value; }
    }
}
