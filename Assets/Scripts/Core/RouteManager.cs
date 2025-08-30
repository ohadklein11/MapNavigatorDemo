using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections;

/// <summary>
/// Manages route calculations and provides a higher-level interface for route operations.
/// Acts as an intermediary between the system and RouteNavigator, handling route management logic.
/// </summary>
public class RouteManager : MonoBehaviour
{
    [Header("Route Navigator Reference")]
    [SerializeField] private RouteNavigator routeNavigator;
    
    [Header("Route Settings")]
    [SerializeField] private LineRenderer routeLineRenderer;
    [SerializeField] private Color routeColor = Color.blue;
    [SerializeField] private float routeWidth = 0.1f;
    [SerializeField] private Material routeMaterial;
    
    [Header("OSRM API Settings")]
    [SerializeField] private float requestTimeout = 10f;
    
    [Header("Route References")]
    [SerializeField] private Transform startPositionObject; // MyPos object
    [SerializeField] private Transform targetPositionObject; // TgtPos object
    [SerializeField] private MapTileGetter mapTileGetter;
    
    [Header("UI References")]
    [SerializeField] private GenericPopup simulatePopUp;
    [SerializeField] private TargetPositionController targetPositionController;
    [SerializeField] private RouteSimulator routeSimulator;
    
    [Header("Debug Settings")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showRoutePoints = false;
    [SerializeField] private GameObject routePointPrefab; // Optional prefab for visualizing route points
    
    [Header("Off-Route Detection")]
    [SerializeField] private bool enableOffRouteDetection = true;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private GameObject pointVisualizationPrefab;
    [SerializeField] private bool showRouteSegmentPoints = true;
    [SerializeField] private float segmentProgressThreshold = 0.8f;
    
    [Header("Player Position History")]
    [SerializeField] private bool showPlayerHistoryPoints = true;
    [SerializeField] private int playerHistoryFrames = 10; // How many frames back for point C
    [SerializeField] private float minimumMovementThreshold = 0.001f; // To trigger direction detection
    [SerializeField] private float maximumMovementThreshold = 30f; // For skipping direction detection
    
    [Header("Course Change Detection")]
    [SerializeField] private bool enableCourseChangeDetection = true;
    [SerializeField] private bool showCourseChangePopup = true;
    [SerializeField] private float courseChangeAngleThreshold = 30f;
    [SerializeField] private int courseChangeIterationThreshold = 10;
    [SerializeField] private bool recalculateRouteOnCourseChange = true;
    [SerializeField] private GenericPopup courseChangePopup;
    [SerializeField] private float courseChangePopupDuration = 3f;

    // Events for route management
    public System.Action<bool> OnRouteCalculated;
    public System.Action OnRouteHidden;
    public System.Action OnRouteStarted;
    public System.Action OnCourseChangeDetected;
    public System.Action OnRouteRecalculated;
    public System.Action<List<Vector3>> OnNewRouteAvailable;
    
    // Current route state
    private bool hasActiveRoute = false;
    private Vector3 currentStartPosition;
    private Vector3 currentTargetPosition;
    
    // Off-route detection state
    private List<Vector3> currentRoutePoints;
    private int currentSegmentIndex = 0; // Index of point 'a' in the route
    private bool isTrackingRoute = false;

    // Visualization GameObjects
    private GameObject pointAObject;
    private GameObject pointBObject;
    private GameObject pointCObject; // Player position X frames ago
    private GameObject pointDObject; // Player current position
    
    // Player position history for point C tracking
    private Queue<Vector3> playerPositionHistory = new Queue<Vector3>();
    private int frameCounter = 0; // Counter to track frames for C/D updates
    private Vector3 lastPointDPosition; // Store the last position of point D

    private SpeedTrackingUtil.SpeedTracker playerSpeedTracker;
    private bool hasHadMeaningfulMovement = false; // Since route started

    private int consecutiveHighAngleCount = 0;
    private bool isCourseChangeDetected = false;
    
    // Course change popup state
    private Coroutine courseChangePopupCoroutine;

    void Awake()
    {
        SetupRouteNavigator();
        InitializeSpeedTracking();
    }
    
    void Start()
    {
        if (!showCourseChangePopup && enableCourseChangeDetection)
        {
            showCourseChangePopup = true;
            if (showDebugInfo)
            {
                Debug.Log("RouteManager: Initialized showCourseChangePopup to true for existing scene compatibility");
            }
        }
        
        if (enableOffRouteDetection && showRouteSegmentPoints)
        {
            InitializeVisualization();
        }
        
        SetupRouteSimulatorSubscriptions();
    }
    
    void Update()
    {
        if (enableOffRouteDetection && isTrackingRoute && hasActiveRoute)
        {
            UpdateCurrentRouteSegment();
        }
        
        // Update off-route detection
        if (enableOffRouteDetection && routeNavigator != null)
        {
            UpdateOffRouteDetection();
        }
        
        // Update player position history and visualization (only when route is active and being tracked)
        if (playerTransform != null && hasActiveRoute && isTrackingRoute)
        {
            UpdatePlayerPositionHistory();
        }
    }
    /// <summary>
    /// Setup the RouteNavigator with all settings and references
    /// </summary>
    private void SetupRouteNavigator()
    {
        if (routeNavigator == null) return;
        
        routeNavigator.SetRouteColor(routeColor);
        routeNavigator.SetRouteWidth(routeWidth);
        routeNavigator.SetRouteMaterial(routeMaterial);
        routeNavigator.SetRequestTimeout(requestTimeout);
        routeNavigator.SetShowDebugInfo(showDebugInfo);
        routeNavigator.SetShowRoutePoints(showRoutePoints);
        routeNavigator.SetRoutePointPrefab(routePointPrefab);
        routeNavigator.SetLineRenderer(routeLineRenderer);
        routeNavigator.SetStartPosition(startPositionObject);
        routeNavigator.SetTargetPosition(targetPositionObject);
        routeNavigator.SetSimulatePopUp(simulatePopUp);
        routeNavigator.SetMapTileGetter(mapTileGetter);
        routeNavigator.SetRouteSimulator(routeSimulator);
        
        if (showDebugInfo)
        {
            Debug.Log("RouteManager: RouteNavigator setup completed");
        }
    }
    
    /// <summary>
    /// Requests a route calculation from start to target position
    /// </summary>
    /// <param name="startPosition">Starting position transform</param>
    /// <param name="targetPosition">Target position transform</param>
    /// <param name="onComplete">Callback when route calculation is complete</param>
    /// <param name="showPopup">Whether to show the SimulatePopup after route calculation (default: true for initial routes, false for recalculations)</param>
    public async void CalculateRoute(Transform startPosition, Transform targetPosition, System.Action<bool> onComplete = null, bool showPopup = true)
    {        
        if (showDebugInfo)
        {
            Debug.Log($"RouteManager: Calculating route from {startPosition.name} to {targetPosition.name}");
        }
        
        currentStartPosition = startPosition.position;
        currentTargetPosition = targetPosition.position;

        startPositionObject = startPosition;
        targetPositionObject = targetPosition;
        
        routeNavigator.SetStartPosition(startPosition);
        routeNavigator.SetTargetPosition(targetPosition);
        
        // Calculate route using RouteNavigator with async handling
        try
        {
            var tcs = new TaskCompletionSource<bool>();
            routeNavigator.CalculateRoute((success) => {
                hasActiveRoute = success;
                
                if (success)
                {
                    StartRouteTracking();
                    var routePoints = routeNavigator.GetCurrentRoutePoints();
                    if (routePoints != null && routePoints.Count > 0)
                    {
                        OnNewRouteAvailable?.Invoke(routePoints);
                        
                        if (showDebugInfo)
                        {
                            Debug.Log($"RouteManager: New route available event fired with {routePoints.Count} points");
                        }
                    }
                }
                else
                {
                    StopRouteTracking();
                }
                
                if (showDebugInfo)
                {
                    Debug.Log($"RouteManager: Route calculation {(success ? "succeeded" : "failed")}");
                }
                OnRouteCalculated?.Invoke(success);
                tcs.SetResult(success);
            }, showPopup);
            
            bool result = await tcs.Task;
            onComplete?.Invoke(result);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"RouteManager: Exception during route calculation: {ex.Message}");
            hasActiveRoute = false;
            StopRouteTracking();
            OnRouteCalculated?.Invoke(false);
            onComplete?.Invoke(false);
        }
    }
    
    /// <summary>
    /// Hides the current route
    /// </summary>
    public void HideRoute()
    {        
        if (showDebugInfo)
        {
            Debug.Log("RouteManager: Hiding route");
        }
        
        routeNavigator.HideRoute();
        hasActiveRoute = false;
        StopRouteTracking();
        OnRouteHidden?.Invoke();
    }
    
    /// <summary>
    /// Checks if there is currently an active route
    /// </summary>
    /// <returns>True if a route is active</returns>
    public bool HasActiveRoute()
    {
        if (routeNavigator == null)
            return false;
        return hasActiveRoute && routeNavigator.IsRouteActive();
    }
    
    void OnDestroy()
    {
        if (routeSimulator != null)
        {
            routeSimulator.OnSimulationCompleted -= OnSimulationCompleted;
        }
        OnRouteCalculated = null;
        OnRouteHidden = null;
        OnRouteStarted = null;
        OnCourseChangeDetected = null;
        OnRouteRecalculated = null;
        OnNewRouteAvailable = null;
        
        if (courseChangePopupCoroutine != null)
        {
            StopCoroutine(courseChangePopupCoroutine);
            courseChangePopupCoroutine = null;
        }
        
        CleanupVisualization();
    }
    
    /// <summary>
    /// Updates the current route segment (points A and B) based on player position
    /// Uses sequential progression instead of finding closest segment for efficiency
    /// </summary>
    private void UpdateCurrentRouteSegment()
    {
        if (currentRoutePoints == null || currentRoutePoints.Count < 2 || playerTransform == null)
        {
            return;
        }
        
        Vector3 playerPosition = playerTransform.position;
        
        bool shouldAdvance = ShouldAdvanceToNextSegment(playerPosition);
        
        if (shouldAdvance && currentSegmentIndex < currentRoutePoints.Count - 2)
        {
            currentSegmentIndex++;
            UpdateSegmentVisualization();
        }
        
        if (showRouteSegmentPoints)
        {
            UpdateVisualizationPositions();
        }
    }
    
    /// <summary>
    /// Determines if the player has progressed far enough to advance to the next route segment
    /// </summary>
    /// <param name="playerPosition">Current player position</param>
    /// <returns>True if should advance to next segment</returns>
    private bool ShouldAdvanceToNextSegment(Vector3 playerPosition)
    {
        if (currentSegmentIndex >= currentRoutePoints.Count - 1)
            return false; // Already at the last segment
        
        Vector3 pointA = currentRoutePoints[currentSegmentIndex];
        Vector3 pointB = currentRoutePoints[currentSegmentIndex + 1];
        float progress = CalculateSegmentProgress(playerPosition, pointA, pointB);
        return progress >= segmentProgressThreshold;
    }
    
    /// <summary>
    /// Calculates how far along a segment the player has progressed (0.0 to 1.0+)
    /// </summary>
    /// <param name="playerPosition">Current player position</param>
    /// <param name="segmentStart">Start point of the segment</param>
    /// <param name="segmentEnd">End point of the segment</param>
    /// <returns>Progress value (0.0 = at start, 1.0 = at end, >1.0 = past end)</returns>
    private float CalculateSegmentProgress(Vector3 playerPosition, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 segmentVector = segmentEnd - segmentStart;
        float segmentLength = segmentVector.magnitude;
        
        if (segmentLength < 0.001f)
        {
            return 1.0f; // Segment has no length, consider it complete
        }
        
        // Project player position onto the segment direction & return progress as a ratio
        Vector3 segmentDirection = segmentVector / segmentLength;
        Vector3 playerVector = playerPosition - segmentStart;
        float projectionLength = Vector3.Dot(playerVector, segmentDirection);
        return projectionLength / segmentLength;
    }
    
    /// <summary>
    /// Updates the visualization of route segment points A and B
    /// </summary>
    private void UpdateSegmentVisualization()
    {
        if (!showRouteSegmentPoints || currentRoutePoints == null || currentRoutePoints.Count < 2)
        {
            HideSegmentVisualization();
            return;
        }
        CreateSegmentVisualization();
        UpdateVisualizationPositions();
    }
    
    /// <summary>
    /// Creates the visualization objects for points A and B
    /// </summary>
    private void CreateSegmentVisualization()
    {
        if (pointVisualizationPrefab == null)
        {
            if (pointAObject == null)
            {
                pointAObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pointAObject.name = "RoutePoint_A";
                
                // Make it green
                Renderer renderer = pointAObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.green;
                }
                
                // Remove collider to avoid interference
                Collider collider = pointAObject.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyImmediate(collider);
                }
            }
            
            if (pointBObject == null)
            {
                pointBObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pointBObject.name = "RoutePoint_B";
                
                // Make it red
                Renderer renderer = pointBObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.red;
                }
                
                // Remove collider to avoid interference
                Collider collider = pointBObject.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyImmediate(collider);
                }
            }
        }
        else
        {
            // Use provided prefabs
            if (pointAObject == null)
            {
                pointAObject = Instantiate(pointVisualizationPrefab);
                pointAObject.name = "RoutePoint_A";
                
                // Try to make it green
                Renderer renderer = pointAObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.green;
                }
            }
            
            if (pointBObject == null)
            {
                pointBObject = Instantiate(pointVisualizationPrefab);
                pointBObject.name = "RoutePoint_B";
                
                // Try to make it red
                Renderer renderer = pointBObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.red;
                }
            }
        }
    }
    
    /// <summary>
    /// Updates the positions of the visualization objects
    /// </summary>
    private void UpdateVisualizationPositions()
    {
        if (currentRoutePoints == null || currentSegmentIndex >= currentRoutePoints.Count - 1)
            return;
        
        Vector3 pointA = currentRoutePoints[currentSegmentIndex];
        Vector3 pointB = currentRoutePoints[currentSegmentIndex + 1];
        
        if (pointAObject != null)
        {
            pointAObject.transform.position = pointA;
            pointAObject.SetActive(true);
        }
        
        if (pointBObject != null)
        {
            pointBObject.transform.position = pointB;
            pointBObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// Hides the segment visualization
    /// </summary>
    private void HideSegmentVisualization()
    {
        if (pointAObject != null)
        {
            pointAObject.SetActive(false);
        }
        
        if (pointBObject != null)
        {
            pointBObject.SetActive(false);
        }
    }

    /// <summary>
    /// Hides the player history visualization (points C and D)
    /// </summary>
    private void HidePlayerHistoryVisualization()
    {
        if (pointCObject != null)
        {
            pointCObject.SetActive(false);
        }
        
        if (pointDObject != null)
        {
            pointDObject.SetActive(false);
        }
    }

    /// <summary>
    /// Starts tracking the route for off-route detection
    /// </summary>
    private void StartRouteTracking()
    {
        if (!enableOffRouteDetection || routeNavigator == null)
            return;
        
        currentRoutePoints = routeNavigator.GetCurrentRoutePoints();
        if (currentRoutePoints == null || currentRoutePoints.Count < 2)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("RouteManager: Cannot start route tracking - insufficient route points");
            }
            return;
        }
        
        // Set up player tracking
        if (playerTransform == null)
        {
            playerTransform = startPositionObject;
        }
        
        if (playerTransform == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("RouteManager: Cannot start route tracking - no player transform found");
            }
            return;
        }
        
        // Reset tracking state
        currentSegmentIndex = 0;
        isTrackingRoute = true;
        hasHadMeaningfulMovement = false; // Reset movement tracking
        consecutiveHighAngleCount = 0;
        isCourseChangeDetected = false;
        
        if (showRouteSegmentPoints)
        {
            UpdateSegmentVisualization();
        }
        InitializeVisualization();
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteManager: Started route tracking with {currentRoutePoints.Count} points");
        }
    }
    
    /// <summary>
    /// Stops tracking the route
    /// </summary>
    private void StopRouteTracking()
    {
        isTrackingRoute = false;
        currentRoutePoints = null;
        currentSegmentIndex = 0;
        frameCounter = 0;
        hasHadMeaningfulMovement = false; // Reset movement tracking
        
        if (playerSpeedTracker != null)
        {
            playerSpeedTracker.Reset();
        }
        
        consecutiveHighAngleCount = 0;
        isCourseChangeDetected = false;
        HideSegmentVisualization();
        HidePlayerHistoryVisualization();
        
        if (showDebugInfo)
        {
            Debug.Log("RouteManager: Stopped route tracking");
        }
    }
    
    /// <summary>
    /// Updates player position history and visualization (only when route is active and being tracked)
    /// </summary>
    private void UpdatePlayerPositionHistory()
    {
        // Safety check: only update history when route is active and being tracked
        if (playerTransform == null || !hasActiveRoute || !isTrackingRoute) return;
        
        Vector3 currentPosition = playerTransform.position;
        
        // Update speed tracking every frame
        if (playerSpeedTracker != null)
        {
            float currentSpeed = playerSpeedTracker.UpdateSpeed(currentPosition, Time.deltaTime);
            
            if (currentSpeed < 0 && showDebugInfo)
            {
                Debug.LogWarning($"RouteManager: Abnormal movement detected and filtered out");
            }
        }
        
        frameCounter++;
        
        // Update points C and D once every playerHistoryFrames frames
        if (frameCounter >= playerHistoryFrames)
        {
            frameCounter = 0;
            
            Vector3 previousPointCPosition = lastPointDPosition; // Store the previous C position
            Vector3 newPointDPosition = currentPosition; // Store the new D position
            
            // Update point C to the previous point D position
            if (pointCObject != null)
            {
                pointCObject.transform.position = lastPointDPosition;
                pointCObject.SetActive(showPlayerHistoryPoints);
            }
            
            // Update point D to current position and store it for next update
            if (pointDObject != null)
            {
                pointDObject.transform.position = currentPosition;
                pointDObject.SetActive(showPlayerHistoryPoints);
            }
            
            // Store current position as the new point D position for next cycle
            lastPointDPosition = currentPosition;
            
            if (showDebugInfo)
            {
                string speedInfo = playerSpeedTracker?.HasSufficientData() == true 
                    ? $", Speed: {playerSpeedTracker.GetAverageSpeed():F2} m/s" 
                    : ", Speed: Insufficient data";
            }
            PerformDirectionDetection(previousPointCPosition, newPointDPosition);
        }
    }

    /// <summary>
    /// Updates off-route detection logic
    /// </summary>
    private void UpdateOffRouteDetection()
    {
        if (routeNavigator == null || playerTransform == null) return;
        
        var route = routeNavigator.GetCurrentRoutePoints();
        if (route == null || route.Count < 2) return;
        
        Vector3 playerPos = playerTransform.position;
        
        // If we don't have a current segment or route has changed, find the closest segment
        if (currentSegmentIndex < 0 || currentSegmentIndex >= route.Count - 1)
        {
            FindClosestSegment(playerPos, route);
        }
        else
        {
            // Check if player has progressed far enough along current segment to advance
            Vector3 segmentStart = route[currentSegmentIndex];
            Vector3 segmentEnd = route[currentSegmentIndex + 1];
            float progress = CalculateSegmentProgress(playerPos, segmentStart, segmentEnd);
            if (progress >= segmentProgressThreshold && currentSegmentIndex < route.Count - 2)
            {
                // Check if player is closer to the next segment
                Vector3 nextSegmentStart = route[currentSegmentIndex + 1];
                Vector3 nextSegmentEnd = route[currentSegmentIndex + 2];
                float currentDistance = DistanceToLineSegment(playerPos, segmentStart, segmentEnd);
                float nextDistance = DistanceToLineSegment(playerPos, nextSegmentStart, nextSegmentEnd);
                
                if (nextDistance < currentDistance)
                {
                    currentSegmentIndex++;
                    if (showDebugInfo)
                    {
                        Debug.Log($"RouteManager: Advanced to segment {currentSegmentIndex}");
                    }
                }
            }
        }
        
        // Update visualization for current segment points (A and B)
        if (showRouteSegmentPoints && currentSegmentIndex >= 0 && currentSegmentIndex < route.Count - 1)
        {
            Vector3 pointA = route[currentSegmentIndex];
            Vector3 pointB = route[currentSegmentIndex + 1];
            
            if (pointAObject != null)
            {
                pointAObject.transform.position = pointA;
                pointAObject.SetActive(true);
            }
            
            if (pointBObject != null)
            {
                pointBObject.transform.position = pointB;
                pointBObject.SetActive(true);
            }
        }
        else
        {
            if (pointAObject != null) pointAObject.SetActive(false);
            if (pointBObject != null) pointBObject.SetActive(false);
        }
    }

    /// <summary>
    /// Finds the closest route segment to the player position
    /// </summary>
    private void FindClosestSegment(Vector3 playerPos, List<Vector3> route)
    {
        float closestDistance = float.MaxValue;
        int closestSegmentIndex = 0;
        
        for (int i = 0; i < route.Count - 1; i++)
        {
            float distance = DistanceToLineSegment(playerPos, route[i], route[i + 1]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestSegmentIndex = i;
            }
        }
        
        currentSegmentIndex = closestSegmentIndex;
        if (showDebugInfo)
        {
            Debug.Log($"RouteManager: Found closest segment: {currentSegmentIndex}");
        }
    }

    /// <summary>
    /// Calculates distance from a point to a line segment
    /// </summary>
    private float DistanceToLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineVector = lineEnd - lineStart;
        Vector3 pointVector = point - lineStart;
        
        float lineLength = lineVector.magnitude;
        if (lineLength < 0.001f) return Vector3.Distance(point, lineStart);
        
        float t = Mathf.Clamp01(Vector3.Dot(pointVector, lineVector) / (lineLength * lineLength));
        Vector3 projection = lineStart + t * lineVector;
        
        return Vector3.Distance(point, projection);
    }

    /// <summary>
    /// Initializes the visualization system
    /// </summary>
    private void InitializeVisualization()
    {
        CleanupVisualization();
        
        if (playerTransform != null)
        {
            Vector3 currentPos = playerTransform.position;
            
            // Initialize both C and D to current position
            lastPointDPosition = currentPos;
            frameCounter = 0;
            
            if (showDebugInfo)
            {
                Debug.Log($"RouteManager: Initialized player history points at position: {currentPos}");
            }
        }
        
        CreateVisualizationObjects();
    }
    
    /// <summary>
    /// Creates visualization objects for route and player tracking
    /// </summary>
    private void CreateVisualizationObjects()
    {
        if (showRouteSegmentPoints)
        {
            if (pointVisualizationPrefab != null)
            {
                pointAObject = Instantiate(pointVisualizationPrefab);
                pointBObject = Instantiate(pointVisualizationPrefab);
            }
            else
            {
                pointAObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pointBObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            }
            
            SetObjectColor(pointAObject, Color.green);
            SetObjectColor(pointBObject, Color.red);            
            pointAObject.name = "RouteSegment_PointA";
            pointBObject.name = "RouteSegment_PointB";
            RemoveCollider(pointAObject);
            RemoveCollider(pointBObject);
        }
        
        if (showPlayerHistoryPoints)
        {
            if (pointVisualizationPrefab != null)
            {
                pointCObject = Instantiate(pointVisualizationPrefab);
                pointDObject = Instantiate(pointVisualizationPrefab);
            }
            else
            {
                pointCObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pointDObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            }
            
            SetObjectColor(pointCObject, Color.yellow);
            SetObjectColor(pointDObject, new Color(1f, 0.5f, 0f)); // Orange
            pointCObject.name = "PlayerHistory_PointC";
            pointDObject.name = "PlayerHistory_PointD";
            RemoveCollider(pointCObject);
            RemoveCollider(pointDObject);
            
            // Set initial positions
            Vector3 currentPos = playerTransform.position;
            pointCObject.transform.position = currentPos;
            pointDObject.transform.position = currentPos;
            pointCObject.SetActive(true);
            pointDObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// Sets the color of a visualization object
    /// </summary>
    private void SetObjectColor(GameObject obj, Color color)
    {
        if (obj != null)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = color;
            }
        }
    }
    
    /// <summary>
    /// Removes collider from visualization object to avoid interference
    /// </summary>
    private void RemoveCollider(GameObject obj)
    {
        if (obj != null)
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }
        }
    }
    
    /// <summary>
    /// Cleans up all visualization objects
    /// </summary>
    private void CleanupVisualization()
    {
        if (pointAObject != null) { DestroyImmediate(pointAObject); pointAObject = null; }
        if (pointBObject != null) { DestroyImmediate(pointBObject); pointBObject = null; }
        if (pointCObject != null) { DestroyImmediate(pointCObject); pointCObject = null; }
        if (pointDObject != null) { DestroyImmediate(pointDObject); pointDObject = null; }
    }

    /// <summary>
    /// Performs direction detection between the current route segment and player movement.
    /// This method is called after each C/D position update to analyze player movement direction.
    /// </summary>
    /// <param name="pointC">Player position X frames ago</param>
    /// <param name="pointD">Current player position</param>
    private void PerformDirectionDetection(Vector3 pointC, Vector3 pointD)
    {
        // Only perform direction detection if we have a valid route segment
        if (currentRoutePoints == null || currentRoutePoints.Count < 2) return;
        if (currentSegmentIndex < 0 || currentSegmentIndex >= currentRoutePoints.Count - 1) return;
        
        // Check if player has moved enough to make direction detection meaningful
        Vector3 playerMovement = pointD - pointC;
        if (playerMovement.magnitude >= minimumMovementThreshold)
        {
            hasHadMeaningfulMovement = true;
        }
        
        if (playerMovement.magnitude < minimumMovementThreshold)
        {
            if (showDebugInfo)
            {
                Debug.Log($"RouteManager [Segment {currentSegmentIndex}]: Skipping direction detection - insufficient player movement ({playerMovement.magnitude:F4}m < {minimumMovementThreshold:F4}m)");
            }
            return;
        }
        
        // Check if player movement is too large (abnormal movement)
        // Calculate the maximum reasonable movement for the given time interval
        float timeInterval = playerHistoryFrames * Time.fixedDeltaTime; // Approximate time for the interval
        float maxReasonableMovement = maximumMovementThreshold * timeInterval;
        
        if (playerMovement.magnitude > maxReasonableMovement)
        {
            if (showDebugInfo)
            {
                Debug.Log($"RouteManager [Segment {currentSegmentIndex}]: Skipping direction detection - abnormal player movement ({playerMovement.magnitude:F4}m > {maxReasonableMovement:F4}m for {timeInterval:F3}s interval)");
            }
            return;
        }
        
        if (!hasHadMeaningfulMovement)
        {
            if (showDebugInfo)
            {
                Debug.Log($"RouteManager [Segment {currentSegmentIndex}]: Skipping direction detection - no meaningful movement since route started");
            }
            return;
        }
        
        Vector3 pointA = currentRoutePoints[currentSegmentIndex];
        Vector3 pointB = currentRoutePoints[currentSegmentIndex + 1];
        
        string speedInfo = "";
        if (playerSpeedTracker != null && playerSpeedTracker.HasSufficientData())
        {
            speedInfo = $" [Speed: {playerSpeedTracker.GetAverageSpeed():F2} m/s]";
        }
        
        float angle = DirectionDetectionUtil.CalculateDirectionAngle(pointA, pointB, pointC, pointD);
        
        // Perform course change detection
        if (enableCourseChangeDetection && !isCourseChangeDetected)
        {
            PerformCourseChangeDetection(angle);
        }
        else if (showDebugInfo)
        {
            if (!enableCourseChangeDetection)
            {
                Debug.Log("RouteManager [Course Change]: Detection skipped - enableCourseChangeDetection is false");
            }
            else if (isCourseChangeDetected)
            {
                Debug.Log("RouteManager [Course Change]: Detection skipped - course change already detected");
            }
        }
    }

    /// <summary>
    /// Performs course change detection based on the angle between route and player movement
    /// </summary>
    /// <param name="angle">Angle in degrees between route segment and player movement vectors</param>
    private void PerformCourseChangeDetection(float angle)
    {
        // Check if angle exceeds threshold
        bool isHighAngle = Mathf.Abs(angle) > courseChangeAngleThreshold;
        
        if (isHighAngle)
        {
            consecutiveHighAngleCount++;
            
            if (showDebugInfo)
            {
                Debug.Log($"RouteManager [Course Change]: High angle detected ({angle:F1}° > {courseChangeAngleThreshold}°) - Count: {consecutiveHighAngleCount}/{courseChangeIterationThreshold}");
            }
            
            // Check if we've reached the iteration threshold
            if (consecutiveHighAngleCount >= courseChangeIterationThreshold)
            {
                DetectCourseChange();
            }
        }
        else
        {
            if (consecutiveHighAngleCount > 0)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"RouteManager [Course Change]: Angle back to normal ({angle:F1}° ≤ {courseChangeAngleThreshold}°) - Resetting counter from {consecutiveHighAngleCount}");
                }
                consecutiveHighAngleCount = 0;
            }
        }
    }
    
    /// <summary>
    /// Handles course change detection - triggers route recalculation and simulation restart
    /// </summary>
    private void DetectCourseChange()
    {
        if (isCourseChangeDetected) return; // Prevent multiple detections
        
        isCourseChangeDetected = true;
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteManager [Course Change]: COURSE CHANGE DETECTED after {consecutiveHighAngleCount} consecutive high-angle iterations!");
        }
        
        ShowCourseChangePopup();
        OnCourseChangeDetected?.Invoke();
        if (recalculateRouteOnCourseChange && playerTransform != null && targetPositionObject != null)
        {
            RecalculateRouteFromCurrentPosition();
        }
    }
    
    /// <summary>
    /// Shows the course change popup for the configured duration
    /// </summary>
    private void ShowCourseChangePopup()
    {
        if (!showCourseChangePopup)
        {
            if (showDebugInfo)
            {
                Debug.Log("RouteManager [Course Change]: Course change popup display is disabled - skipping popup");
            }
            return;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteManager [Course Change]: Showing course change popup for {courseChangePopupDuration} seconds");
        }
        
        if (courseChangePopupCoroutine != null)
        {
            StopCoroutine(courseChangePopupCoroutine);
            courseChangePopupCoroutine = null;
        }
        
        courseChangePopup.ShowPopup(true);
        
        if (courseChangePopupDuration > 0f)
        {
            courseChangePopupCoroutine = StartCoroutine(HideCourseChangePopupAfterDelay());
        }
    }
    
    /// <summary>
    /// Coroutine to automatically hide the course change popup after the configured duration
    /// </summary>
    private IEnumerator HideCourseChangePopupAfterDelay()
    {
        yield return new WaitForSeconds(courseChangePopupDuration);
        
        if (courseChangePopup != null && courseChangePopup.IsVisible())
        {
            if (showDebugInfo)
            {
                Debug.Log("RouteManager [Course Change]: Auto-hiding course change popup after duration");
            }
            
            courseChangePopup.HidePopup(true);
        }
        
        courseChangePopupCoroutine = null;
    }
    
    /// <summary>
    /// Recalculates the route from the player's current position to the target
    /// </summary>
    private async void RecalculateRouteFromCurrentPosition()
    {
        if (playerTransform == null || targetPositionObject == null)
        {
            Debug.LogError("RouteManager [Course Change]: Cannot recalculate route - missing player or target transform");
            return;
        }
        
        Vector3 currentPlayerPosition = playerTransform.position;
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteManager [Course Change]: Recalculating route from current position {currentPlayerPosition} to target {targetPositionObject.position}");
        }
                
        if (startPositionObject != null)
        {
            startPositionObject.position = currentPlayerPosition;
        }
        
        // Fire route recalculated event BEFORE calculating to ensure RouteSimulator gets the flag set
        OnRouteRecalculated?.Invoke();
        
        if (showDebugInfo)
        {
            Debug.Log("RouteManager [Course Change]: Route recalculation event fired - RouteSimulator will auto-start next route");
        }
        
        // Recalculate route
        try
        {
            var tcs = new TaskCompletionSource<bool>();
            CalculateRoute(startPositionObject, targetPositionObject, (success) => {
                tcs.SetResult(success);
            }, showPopup: false);
            bool success = await tcs.Task;
            
            if (success)
            {
                if (showDebugInfo)
                {
                    Debug.Log("RouteManager [Course Change]: Route recalculation successful");
                }
            }
            else
            {
                Debug.LogError("RouteManager [Course Change]: Failed to recalculate route");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"RouteManager [Course Change]: Exception during route recalculation: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes the speed tracking system
    /// </summary>
    private void InitializeSpeedTracking()
    {
        // Create speed tracker with 100 frame history and use the configured maximum movement threshold
        // This ensures consistency between speed tracking and direction detection thresholds
        playerSpeedTracker = SpeedTrackingUtil.CreateTracker(maxHistoryFrames: 100, maxReasonableSpeed: maximumMovementThreshold);
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteManager: Initialized speed tracking with 100-frame history and {maximumMovementThreshold} m/s max speed");
        }
    }
    
    /// <summary>
    /// Setup subscriptions to RouteSimulator events for handling simulation completion
    /// </summary>
    private void SetupRouteSimulatorSubscriptions()
    {
        routeSimulator.OnSimulationCompleted += OnSimulationCompleted;
        
        if (showDebugInfo)
        {
            Debug.Log("RouteManager: Subscribed to RouteSimulator events");
        }
    }
    
    /// <summary>
    /// Event handler for when RouteSimulator completes simulation
    /// Automatically hides the route and cleans up visual elements
    /// </summary>
    private void OnSimulationCompleted()
    {
        if (showDebugInfo)
        {
            Debug.Log("RouteManager: Simulation completed - hiding route and cleaning up");
        }
        
        if (hasActiveRoute)
        {
            HideRoute();
            
            if (showDebugInfo)
            {
                Debug.Log("RouteManager: Route hidden after simulation completion");
            }
        }
        
        targetPositionController.HideTargetMarker();
            
        if (showDebugInfo)
        {
            Debug.Log("RouteManager: Target marker hidden after simulation completion");
        }
    }
    
    #region Settings Control Methods (for SettingsManager)
    
    /// <summary>
    /// Sets whether course change popup is enabled
    /// Called by SettingsManager to control course change popup visibility
    /// <summary>
    /// Controls whether the course change popup is shown when course changes are detected.
    /// The detection logic continues to run regardless of this setting.
    /// </summary>
    /// <param name="enabled">True to show course change popup, false to hide it</param>
    public void SetCourseChangePopupEnabled(bool enabled)
    {
        if (showDebugInfo)
        {
            Debug.Log($"RouteManager: SetCourseChangePopupEnabled called with enabled={enabled}. " +
                     $"Current values: enableCourseChangeDetection={enableCourseChangeDetection}, " +
                     $"showCourseChangePopup={showCourseChangePopup}");
        }
        
        showCourseChangePopup = enabled;
        
        if (!enabled && courseChangePopup != null && courseChangePopup.IsVisible())
        {
            courseChangePopup.HidePopup();
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteManager: Course change popup {(enabled ? "enabled" : "disabled")} via SettingsManager " +
                     $"(detection logic still runs: enableCourseChangeDetection={enableCourseChangeDetection})");
        }
    }
    
    /// <summary>
    /// Sets whether route segment points (A and B) are visible
    /// Called by SettingsManager to control debug points visibility
    /// </summary>
    /// <param name="visible">True to show route segment points, false to hide</param>
    public void SetShowRouteSegmentPoints(bool visible)
    {
        showRouteSegmentPoints = visible;
        
        if (pointAObject != null)
        {
            pointAObject.SetActive(visible && enableOffRouteDetection && isTrackingRoute);
        }
        
        if (pointBObject != null)
        {
            pointBObject.SetActive(visible && enableOffRouteDetection && isTrackingRoute);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteManager: Route segment points (A,B) {(visible ? "enabled" : "disabled")} via SettingsManager");
        }
    }
    
    /// <summary>
    /// Sets whether player history points (C and D) are visible
    /// Called by SettingsManager to control debug points visibility
    /// </summary>
    /// <param name="visible">True to show player history points, false to hide</param>
    public void SetShowPlayerHistoryPoints(bool visible)
    {
        showPlayerHistoryPoints = visible;
        
        if (pointCObject != null)
        {
            pointCObject.SetActive(visible && enableOffRouteDetection && isTrackingRoute);
        }
        
        if (pointDObject != null)
        {
            pointDObject.SetActive(visible && enableOffRouteDetection && isTrackingRoute);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteManager: Player history points (C,D) {(visible ? "enabled" : "disabled")} via SettingsManager");
        }
    }
    
    #endregion
}
