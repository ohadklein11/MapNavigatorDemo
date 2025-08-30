using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles route calculation using centralized OSRMService and route visualization using LineRenderer
/// Managed entirely by RouteManager script.
/// </summary>
public class RouteNavigator : MonoBehaviour
{
    // Route Settings
    private LineRenderer routeLineRenderer;
    private Color routeColor = Color.blue;
    private float routeWidth = 0.1f;
    private Material routeMaterial;
    
    // OSRM API Settings
    private float requestTimeout = 10f;
    
    // Debug Settings
    private bool showDebugInfo = true;
    private bool showRoutePoints = false;
    private GameObject routePointPrefab;
    
    // Internal references
    private Transform startPositionObject; // MyPos object
    private Transform targetPositionObject; // TgtPos object
    private GenericPopup simulatePopUp;
    private MapTileGetter mapTileGetter;
    private RouteSimulator routeSimulator;
    private TargetPositionController targetPositionController;
    private List<GameObject> routePointObjects = new List<GameObject>();
    private bool isRouteActive = false;
    private bool hasDisplayedFirstRoute = false; // For camera focusing
    
    void Awake()
    {
        // Setup LineRenderer if not assigned
        if (routeLineRenderer == null)
        {
            routeLineRenderer = GetComponent<LineRenderer>();
            if (routeLineRenderer == null)
            {
                routeLineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }
        
        SetupLineRenderer();
    }
    
    void Start()
    {
        HideRoute();
    }
    
    /// <summary>
    /// Setup the LineRenderer properties
    /// </summary>
    void SetupLineRenderer()
    {
        if (routeLineRenderer == null) return;
        
        routeLineRenderer.startColor = routeColor;
        routeLineRenderer.endColor = routeColor;
        routeLineRenderer.startWidth = routeWidth;
        routeLineRenderer.endWidth = routeWidth;
        routeLineRenderer.useWorldSpace = true;
        routeLineRenderer.sortingOrder = 1; // Render above map tiles
        
        if (routeMaterial != null)
        {
            routeLineRenderer.material = routeMaterial;
        }
        
        routeLineRenderer.enabled = false; // Initially disable
    }
    
    /// <summary>
    /// Calculates and displays route from start to target position using centralized OSRMService
    /// </summary>
    /// <param name="onRouteCalculated">Callback when route calculation is complete</param>
    /// <param name="showPopupAfterCalculation">Whether to show the SimulatePopup after route calculation (default: true for initial routes, false for recalculations)</param>
    public void CalculateRoute(System.Action<bool> onRouteCalculated = null, bool showPopupAfterCalculation = true)
    {
        Debug.Log($"Calculating route from {startPositionObject.position} to {targetPositionObject.position}");

        // Convert world positions to GPS coordinates
        Vector2 startGPS = mapTileGetter.WorldToGPSCoordinates(startPositionObject.position);
        Vector2 endGPS = mapTileGetter.WorldToGPSCoordinates(targetPositionObject.position);
        
        OSRMService.Instance.CalculateRoute(
            startGPS,
            endGPS,
            (success, routePoints) => {
                if (success && routePoints != null && routePoints.Count > 0)
                {
                    ProcessRoutePoints(routePoints, showPopupAfterCalculation);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"Route calculated successfully! Points: {routePoints.Count}");
                    }
                    
                    onRouteCalculated?.Invoke(true);
                }
                else
                {
                    Debug.LogError("RouteNavigator: Failed to calculate route via OSRMService");
                    onRouteCalculated?.Invoke(false);
                }
            },
            requestTimeout
        );
    }
    
    /// <summary>
    /// Processes route points and creates the visual representation
    /// </summary>
    /// <param name="routePoints">List of route points to process</param>
    /// <param name="showPopupAfterProcessing">Whether to show the SimulatePopup after processing</param>
    void ProcessRoutePoints(List<Vector3> routePoints, bool showPopupAfterProcessing = true)
    {   
        if (showDebugInfo)
        {
            Debug.Log($"Route has {routePoints.Count} points");
        }
        
        RepositionMarkersToRouteEndpoints(routePoints, showPopupAfterProcessing);
        
        // Center camera and zoom to show full route
        if (!hasDisplayedFirstRoute)
        {
            CenterCameraOnRoute(routePoints);
            hasDisplayedFirstRoute = true; // Mark that we've focused the camera once
            
            if (showDebugInfo)
            {
                Debug.Log("RouteNavigator: Centered camera on first route");
            }
        }
        else if (showDebugInfo)
        {
            Debug.Log("RouteNavigator: Skipping camera focus - not the first route");
        }
        
        if (showPopupAfterProcessing)
        {
            EnableSimulatePopUp();
        }
        
        DisplayRoute(routePoints);
        
        if (showRoutePoints)
        {
            ShowRoutePoints(routePoints);
        }
    }
    
    /// <summary>
    /// Displays the route using LineRenderer
    /// </summary>
    void DisplayRoute(List<Vector3> routePoints)
    {
        if (routeLineRenderer == null || routePoints == null || routePoints.Count < 2)
        {
            Debug.LogWarning("Cannot display route: LineRenderer or route points invalid");
            return;
        }
        
        // Set all the points
        routeLineRenderer.positionCount = routePoints.Count;
        for (int i = 0; i < routePoints.Count; i++)
        {
            routeLineRenderer.SetPosition(i, routePoints[i]);
        }
        routeLineRenderer.enabled = true;
        isRouteActive = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"Route displayed with {routePoints.Count} points");
        }
    }
    
    /// <summary>
    /// Shows route points as visual markers (optional debugging feature)
    /// </summary>
    void ShowRoutePoints(List<Vector3> routePoints)
    {
        ClearRoutePoints();
        
        if (routePointPrefab == null) return;
        
        foreach (Vector3 point in routePoints)
        {
            GameObject routePoint = Instantiate(routePointPrefab, point, Quaternion.identity, transform);
            routePointObjects.Add(routePoint);
        }
    }
    
    /// <summary>
    /// Clears all route point visual markers
    /// </summary>
    void ClearRoutePoints()
    {
        foreach (GameObject obj in routePointObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        routePointObjects.Clear();
    }
    
    /// <summary>
    /// Hides the route
    /// </summary>
    public void HideRoute()
    {
        routeLineRenderer.enabled = false;
        ClearRoutePoints();
        isRouteActive = false;
        hasDisplayedFirstRoute = false;
        DisableSimulatePopUp();
        
        if (showDebugInfo)
        {
            Debug.Log("Route hidden - reset camera focus flag");
        }
    }
    
    /// <summary>
    /// Returns whether a route is currently active/visible
    /// </summary>
    public bool IsRouteActive()
    {
        return isRouteActive;
    }
    
    /// <summary>
    /// Sets the start position object reference
    /// </summary>
    public void SetStartPosition(Transform startPos)
    {
        startPositionObject = startPos;
    }
    
    /// <summary>
    /// Sets the target position object reference
    /// </summary>
    public void SetTargetPosition(Transform targetPos)
    {
        targetPositionObject = targetPos;
    }
    
    /// <summary>
    /// Sets the route color
    /// </summary>
    public void SetRouteColor(Color color)
    {
        routeColor = color;
        if (routeLineRenderer != null)
        {
            routeLineRenderer.startColor = color;
            routeLineRenderer.endColor = color;
        }
    }
    
    /// <summary>
    /// Sets the route width
    /// </summary>
    public void SetRouteWidth(float width)
    {
        routeWidth = width;
        if (routeLineRenderer != null)
        {
            routeLineRenderer.startWidth = width;
            routeLineRenderer.endWidth = width;
        }
    }

    /// <summary>
    /// Repositions the start and target position markers to the actual route endpoints
    /// </summary>
    /// <param name="routePoints">List of route points</param>
    /// <param name="isInitialRoute">Whether this is an initial route (true) or a recalculation (false)</param>
    void RepositionMarkersToRouteEndpoints(List<Vector3> routePoints, bool isInitialRoute = true)
    {
        if (routePoints == null || routePoints.Count < 2)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("Not enough route points to reposition markers");
            }
            return;
        }
        
        Vector3 routeStartPoint = routePoints[0];
        Vector3 routeEndPoint = routePoints[routePoints.Count - 1];
        
        // Check if RouteSimulator is currently running a simulation
        bool isSimulationActive = routeSimulator.IsSimulating();
        
        // Don't reposition if simulation is currently active
        bool shouldRepositionStartMarker = !isSimulationActive && isInitialRoute;
        if (startPositionObject != null && shouldRepositionStartMarker)
        {
            Vector3 oldStartPos = startPositionObject.position;
            startPositionObject.position = routeStartPoint;
            
            if (showDebugInfo)
            {
                float distance = Vector3.Distance(oldStartPos, routeStartPoint);
                Debug.Log($"Repositioned start marker from {oldStartPos} to {routeStartPoint} (moved {distance:F3} units)");
            }
        }
        else if (startPositionObject != null)
        {
            if (showDebugInfo)
            {
                string reason = !isInitialRoute ? "route recalculation" : "active simulation";
                Debug.Log($"Skipped repositioning start marker during {reason} - keeping player at current position {startPositionObject.position}");
            }
        }
        
        // Always reposition target marker (TgtPos) to last route point
        if (targetPositionObject != null)
        {
            Vector3 oldTargetPos = targetPositionObject.position;
            targetPositionObject.position = routeEndPoint;
            
            if (showDebugInfo)
            {
                float distance = Vector3.Distance(oldTargetPos, routeEndPoint);
                Debug.Log($"Repositioned target marker from {oldTargetPos} to {routeEndPoint} (moved {distance:F3} units)");
            }
        }
    }
    
    /// <summary>
    /// Centers the camera on the route and adjusts zoom to show the full route
    /// </summary>
    void CenterCameraOnRoute(List<Vector3> routePoints)
    {
        if (routePoints == null || routePoints.Count == 0)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("No route points to center camera on");
            }
            return;
        }
        
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }
        
        if (mainCamera == null)
        {
            Debug.LogWarning("No camera found to center on route");
            return;
        }
        
        // Calculate bounds of the route
        Vector3 minBounds = routePoints[0];
        Vector3 maxBounds = routePoints[0];
        
        foreach (Vector3 point in routePoints)
        {
            minBounds.x = Mathf.Min(minBounds.x, point.x);
            minBounds.y = Mathf.Min(minBounds.y, point.y);
            maxBounds.x = Mathf.Max(maxBounds.x, point.x);
            maxBounds.y = Mathf.Max(maxBounds.y, point.y);
        }
        
        // Calculate center point
        Vector3 centerPoint = (minBounds + maxBounds) * 0.5f;
        
        // Calculate required camera size to fit the route
        float routeWidth = maxBounds.x - minBounds.x;
        float routeHeight = maxBounds.y - minBounds.y;
        
        // Add some padding (20% extra space around the route)
        float padding = 0.2f;
        routeWidth *= (1f + padding);
        routeHeight *= (1f + padding);
        
        // Calculate required size
        float requiredSize = Mathf.Max(routeHeight * 0.5f, routeWidth * 0.5f / mainCamera.aspect);
        float minCameraSize = 0.5f;
        requiredSize = Mathf.Max(requiredSize, minCameraSize);
        
        // Position camera at center point, keeping Z position
        Vector3 newCameraPos = mainCamera.transform.position;
        newCameraPos.x = centerPoint.x;
        newCameraPos.y = centerPoint.y;
        mainCamera.transform.position = newCameraPos;
        mainCamera.orthographicSize = requiredSize;
        
        if (showDebugInfo)
        {
            Debug.Log($"Centered camera on route: Center({centerPoint.x:F2}, {centerPoint.y:F2}), " +
                     $"Size: {requiredSize:F2}, Bounds: ({minBounds.x:F2}, {minBounds.y:F2}) to ({maxBounds.x:F2}, {maxBounds.y:F2})");
        }
    }

    /// <summary>
    /// Enables the SimulatePopUp panel when route is ready
    /// </summary>
    void EnableSimulatePopUp()
    {
        simulatePopUp.OnCloseButtonClicked -= OnSimulatePopUpClosed;
        simulatePopUp.OnActionButtonClicked -= OnSimulatePopUpActionClicked;
        simulatePopUp.OnCloseButtonClicked += OnSimulatePopUpClosed;
        simulatePopUp.OnActionButtonClicked += OnSimulatePopUpActionClicked;
        simulatePopUp.ShowPopup();
        
        if (showDebugInfo)
        {
            Debug.Log("SimulatePopUp panel enabled - route is ready");
        }
    }
    
    /// <summary>
    /// Disables the SimulatePopUp panel when route is hidden
    /// </summary>
    void DisableSimulatePopUp()
    {
        simulatePopUp.OnCloseButtonClicked -= OnSimulatePopUpClosed;
        simulatePopUp.OnActionButtonClicked -= OnSimulatePopUpActionClicked;
        simulatePopUp.HidePopup();
        
        if (showDebugInfo)
        {
            Debug.Log("SimulatePopUp panel disabled - route hidden");
        }
    }
    
    /// <summary>
    /// Called when SimulatePopUp close button is clicked
    /// </summary>
    void OnSimulatePopUpClosed()
    {
        if (showDebugInfo)
        {
            Debug.Log("SimulatePopUp closed by user - hiding route");
        }
        HideRoute();
    }

    /// <summary>
    /// Called when SimulatePopUp action button is clicked (Simulate Drive button)
    /// </summary>
    void OnSimulatePopUpActionClicked(int buttonIndex)
    {
        // The only action button is the "Simulate Drive" button
        if (buttonIndex == 0)
        {
            if (showDebugInfo)
            {
                Debug.Log("Simulate Drive button clicked - finding RouteSimulator to start manual simulation");
            }
            
            routeSimulator.StartSimulationManually();
        }
    }
    
    /// <summary>
    /// Gets the current route points for external use (e.g., off-route detection)
    /// </summary>
    /// <returns>List of route points, or null if no route is active</returns>
    public List<Vector3> GetCurrentRoutePoints()
    {
        if (!isRouteActive)
            return null;
        
        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i < routeLineRenderer.positionCount; i++)
        {
            points.Add(routeLineRenderer.GetPosition(i));
        }
        
        return points;
    }

    /// <summary>
    /// Sets the SimulatePopUp reference
    /// </summary>
    public void SetSimulatePopUp(GenericPopup popup)
    {
        simulatePopUp = popup;
    }

    /// <summary>
    /// Sets the MapTileGetter reference
    /// </summary>
    public void SetMapTileGetter(MapTileGetter getter)
    {
        mapTileGetter = getter;
    }

    /// <summary>
    /// Sets the RouteSimulator reference
    /// </summary>
    /// <param name="simulator">RouteSimulator component</param>
    public void SetRouteSimulator(RouteSimulator simulator)
    {
        routeSimulator = simulator;
    }

    /// <summary>
    /// Sets the LineRenderer reference
    /// </summary>
    /// <param name="lineRenderer">LineRenderer component</param>
    public void SetLineRenderer(LineRenderer lineRenderer)
    {
        routeLineRenderer = lineRenderer;
        if (routeLineRenderer != null)
        {
            SetupLineRenderer();
        }
    }
    
    /// <summary>
    /// Sets the route material
    /// </summary>
    /// <param name="material">Material for the route line</param>
    public void SetRouteMaterial(Material material)
    {
        routeMaterial = material;
        if (routeLineRenderer != null && routeMaterial != null)
        {
            routeLineRenderer.material = routeMaterial;
        }
    }
    
    /// <summary>
    /// Sets the request timeout
    /// </summary>
    /// <param name="timeout">Timeout in seconds</param>
    public void SetRequestTimeout(float timeout)
    {
        requestTimeout = timeout;
    }
    
    /// <summary>
    /// Sets the debug info visibility
    /// </summary>
    /// <param name="show">Whether to show debug info</param>
    public void SetShowDebugInfo(bool show)
    {
        showDebugInfo = show;
    }
    
    /// <summary>
    /// Sets whether to show route points
    /// </summary>
    /// <param name="show">Whether to show route points</param>
    public void SetShowRoutePoints(bool show)
    {
        showRoutePoints = show;
    }
    
    /// <summary>
    /// Sets the route point prefab
    /// </summary>
    /// <param name="prefab">Prefab for route point visualization</param>
    public void SetRoutePointPrefab(GameObject prefab)
    {
        routePointPrefab = prefab;
    }
}
