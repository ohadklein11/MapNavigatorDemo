using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles randomization of simulation routes during route simulation.
/// Listens for route segment changes and randomly decides to create deviation points.
/// </summary>
public class SimulationRouteRandomizer : MonoBehaviour
{
    [Header("Randomization Settings")]
    [SerializeField] private float randomPointRadius = 3f;
    [SerializeField] private bool enableRandomization = true;
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private float randomizationDistanceInterval = 1f;
    
    [Header("Visualization")]
    [SerializeField] private GameObject randomPointPrefab;
    [SerializeField] private Color randomPointColor = Color.red;
    [SerializeField] private bool showRandomPoints = true;
    [SerializeField] private float randomPointDisplayDuration = 5f;

    [Header("References")]
    [SerializeField] private RouteSimulator routeSimulator;
    [SerializeField] private RouteManager routeManager;
    [SerializeField] private GenericPopup simulatePopup;
    [SerializeField] private Transform playerTransform;
    
    private List<GameObject> activeRandomPoints = new List<GameObject>();
    private bool isSubscribedToEvents = false;
    private bool hasGeneratedPointForCurrentRoute = false;
    private bool isRandomRouteRequestPending = false;
    private int currentRandomRouteRequestId = 0;

    private Vector3 lastPlayerPosition;
    private float totalDistanceTraveled = 0f;
    private float nextRandomizationDistance = 0f;
    private bool isTrackingDistance = false;

    public System.Action<Vector3> OnRandomPointGenerated;
    public System.Action<List<Vector3>> OnRandomRouteReady;

    void Awake()
    {   
        if (randomPointPrefab == null)
        {
            CreateDefaultRandomPointPrefab();
        }
    }
    
    void Start()
    {
        SubscribeToEvents();
    }
    
    void OnDestroy()
    {
        UnsubscribeFromEvents();
        CleanupRandomPoints();
    }
    
    void Update()
    {
        if (isTrackingDistance && enableRandomization && playerTransform != null)
        {
            TrackDistanceForRandomization();
        }
    }
    
    /// <summary>
    /// Subscribe to RouteSimulator events
    /// </summary>
    private void SubscribeToEvents()
    {
        if (routeSimulator != null && !isSubscribedToEvents)
        {
            routeSimulator.OnPointReached += OnRoutePointReached;
            routeSimulator.OnSimulationStarted += OnSimulationStarted;
            routeSimulator.OnSimulationStopped += OnSimulationStopped;
            routeSimulator.OnSimulationCompleted += OnSimulationCompleted;
            
            isSubscribedToEvents = true;
            
            if (showDebugInfo)
            {
                Debug.Log("SimulationRouteRandomizer: Subscribed to RouteSimulator events");
            }
        }
        
        routeManager.OnNewRouteAvailable += OnNewRouteAvailable;
        routeManager.OnRouteRecalculated += OnRouteRecalculated;
        
        if (showDebugInfo)
        {
            Debug.Log("SimulationRouteRandomizer: Subscribed to RouteManager events");
        }
    }
    
    /// <summary>
    /// Unsubscribe from RouteSimulator events
    /// </summary>
    private void UnsubscribeFromEvents()
    {
        if (routeSimulator != null && isSubscribedToEvents)
        {
            routeSimulator.OnPointReached -= OnRoutePointReached;
            routeSimulator.OnSimulationStarted -= OnSimulationStarted;
            routeSimulator.OnSimulationStopped -= OnSimulationStopped;
            routeSimulator.OnSimulationCompleted -= OnSimulationCompleted;
            
            isSubscribedToEvents = false;
            
            if (showDebugInfo)
            {
                Debug.Log("SimulationRouteRandomizer: Unsubscribed from RouteSimulator events");
            }
        }
        
        routeManager.OnNewRouteAvailable -= OnNewRouteAvailable;
        routeManager.OnRouteRecalculated -= OnRouteRecalculated;
        
        if (showDebugInfo)
        {
            Debug.Log("SimulationRouteRandomizer: Unsubscribed from RouteManager events");
        }
    }
    
    /// <summary>
    /// Called when simulation starts - clean up any existing random points and reset route state
    /// </summary>
    private void OnSimulationStarted()
    {
        if (showDebugInfo)
        {
            Debug.Log("SimulationRouteRandomizer: Simulation started - cleaning up existing random points and resetting route state");
        }
        
        CleanupRandomPoints();
        hasGeneratedPointForCurrentRoute = false;
        CancelPendingRandomRouteRequest();
        StartDistanceTracking();
    }
    
    /// <summary>
    /// Called when simulation stops - clean up random points and reset route state
    /// </summary>
    private void OnSimulationStopped()
    {
        if (showDebugInfo)
        {
            Debug.Log("SimulationRouteRandomizer: Simulation stopped - cleaning up random points and resetting route state");
        }
        
        CleanupRandomPoints();
        hasGeneratedPointForCurrentRoute = false;
        CancelPendingRandomRouteRequest();
        StopDistanceTracking();
    }
    
    /// <summary>
    /// Called when simulation completes - clean up random points and reset route state
    /// </summary>
    private void OnSimulationCompleted()
    {
        if (showDebugInfo)
        {
            Debug.Log("SimulationRouteRandomizer: Simulation completed - cleaning up random points and resetting route state");
        }
        
        CleanupRandomPoints();
        hasGeneratedPointForCurrentRoute = false;
        CancelPendingRandomRouteRequest();
        StopDistanceTracking();
    }
    
    /// <summary>
    /// Called when RouteSimulator reaches a new point in the route
    /// This is where we check for randomization
    /// </summary>
    /// <param name="currentPoint">Current point index</param>
    /// <param name="totalPoints">Total number of points in route</param>
    private void OnRoutePointReached(int currentPoint, int totalPoints)
    {
        if (showDebugInfo)
        {
            Debug.Log($"SimulationRouteRandomizer: Point {currentPoint}/{totalPoints} reached (using distance-based randomization now)");
        }
    }
    
    /// <summary>
    /// Gets the randomization probability from the popup slider
    /// </summary>
    /// <returns>Probability value between 0.0 and 1.0</returns>
    private float GetRandomizationProbability()
    {
        return simulatePopup.GetRandomizationValue();
    }
    
    /// <summary>
    /// Generates a random point exactly on the circle circumference around the player
    /// and requests a route calculation to that point
    /// </summary>
    private async void GenerateRandomPoint()
    {
        if (isRandomRouteRequestPending)
        {
            if (showDebugInfo)
            {
                Debug.Log("SimulationRouteRandomizer: Random route request already pending, skipping new generation");
            }
            return;
        }
        
        Vector3 playerPosition = playerTransform.position;
        
        float randomAngle = Random.Range(0f, 2f * Mathf.PI);
        Vector2 randomDirection = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
        Vector2 randomOffset2D = randomDirection * randomPointRadius;
        Vector3 randomPoint = playerPosition + new Vector3(randomOffset2D.x, randomOffset2D.y, 0f);
        
        if (showDebugInfo)
        {
            Debug.Log($"SimulationRouteRandomizer: Generated random point at {randomPoint} (radius: {randomPointRadius}, player: {playerPosition}) - requesting route");
        }
        
        if (showRandomPoints)
        {
            DisplayRandomPoint(randomPoint);
        }
        
        OnRandomPointGenerated?.Invoke(randomPoint);
        
        await RequestRandomRoute(playerPosition, randomPoint);
    }
    
    /// <summary>
    /// Displays a random point in the scene
    /// </summary>
    /// <param name="position">Position to display the random point</param>
    private void DisplayRandomPoint(Vector3 position)
    {
        if (randomPointPrefab == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("SimulationRouteRandomizer: Random point prefab is null, cannot display point");
            }
            return;
        }

        GameObject randomPointObject = Instantiate(randomPointPrefab, position, Quaternion.identity);
        randomPointObject.name = $"RandomPoint_{activeRandomPoints.Count}";        
        Renderer renderer = randomPointObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = randomPointColor;
        }
        
        activeRandomPoints.Add(randomPointObject);
        
        StartCoroutine(RemoveRandomPointAfterDelay(randomPointObject, randomPointDisplayDuration));
        
        if (showDebugInfo)
        {
            Debug.Log($"SimulationRouteRandomizer: Displayed random point at {position} (will be removed after {randomPointDisplayDuration}s)");
        }
    }
    
    /// <summary>
    /// Removes a random point after the specified delay
    /// </summary>
    /// <param name="randomPointObject">The random point object to remove</param>
    /// <param name="delay">Delay in seconds</param>
    private System.Collections.IEnumerator RemoveRandomPointAfterDelay(GameObject randomPointObject, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (randomPointObject != null)
        {
            activeRandomPoints.Remove(randomPointObject);
            Destroy(randomPointObject);
            
            if (showDebugInfo)
            {
                Debug.Log("SimulationRouteRandomizer: Removed random point after delay");
            }
        }
    }
    
    /// <summary>
    /// Cleans up all active random points
    /// </summary>
    private void CleanupRandomPoints()
    {
        foreach (GameObject randomPoint in activeRandomPoints)
        {
            if (randomPoint != null)
            {
                Destroy(randomPoint);
            }
        }
        
        activeRandomPoints.Clear();
        
        if (showDebugInfo && activeRandomPoints.Count > 0)
        {
            Debug.Log("SimulationRouteRandomizer: Cleaned up all random points");
        }
    }
    
    /// <summary>
    /// Creates a default random point prefab if none is assigned
    /// </summary>
    private void CreateDefaultRandomPointPrefab()
    {
        GameObject defaultPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        defaultPrefab.transform.localScale = Vector3.one * 0.5f;
        Renderer renderer = defaultPrefab.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = randomPointColor;
        }        
        defaultPrefab.SetActive(false);
        randomPointPrefab = defaultPrefab;
        
        if (showDebugInfo)
        {
            Debug.Log("SimulationRouteRandomizer: Created default random point prefab (red sphere)");
        }
    }
    
    /// <summary>
    /// Called when RouteManager provides a new route - reset randomization state
    /// </summary>
    /// <param name="routePoints">The new route points</param>
    private void OnNewRouteAvailable(List<Vector3> routePoints)
    {
        if (showDebugInfo)
        {
            Debug.Log("SimulationRouteRandomizer: New route available - resetting randomization state");
        }
        
        hasGeneratedPointForCurrentRoute = false;
        CancelPendingRandomRouteRequest();
    }

    /// <summary>
    /// Called when RouteManager recalculates the route - reset randomization state
    /// </summary>
    private void OnRouteRecalculated()
    {
        if (showDebugInfo)
        {
            Debug.Log("SimulationRouteRandomizer: Route recalculated - resetting randomization state");
        }

        hasGeneratedPointForCurrentRoute = false;
        CancelPendingRandomRouteRequest();
    }
    
    /// <summary>
    /// Requests a random route from the player's current position to the random point
    /// </summary>
    /// <param name="startPosition">Player's current position</param>
    /// <param name="randomDestination">Random destination point</param>
    private async System.Threading.Tasks.Task RequestRandomRoute(Vector3 startPosition, Vector3 randomDestination)
    {
        if (isRandomRouteRequestPending)
        {
            if (showDebugInfo)
            {
                Debug.Log("SimulationRouteRandomizer: Random route request already in progress");
            }
            return;
        }
        
        isRandomRouteRequestPending = true;
        int requestId = ++currentRandomRouteRequestId;
        
        if (showDebugInfo)
        {
            Debug.Log($"SimulationRouteRandomizer: Requesting random route #{requestId} from {startPosition} to {randomDestination}");
        }
        
        try
        {
            var result = await OSRMService.Instance.CalculateRouteFromWorldPositionsAsync(startPosition, randomDestination);
            if (requestId != currentRandomRouteRequestId)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"SimulationRouteRandomizer: Ignoring outdated random route response #{requestId} (current: #{currentRandomRouteRequestId})");
                }
                return;
            }
            
            isRandomRouteRequestPending = false;
            
            if (result.success && result.routePoints != null && result.routePoints.Count > 0)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"SimulationRouteRandomizer: Random route #{requestId} calculated successfully with {result.routePoints.Count} points");
                }                
                OnRandomRouteReady?.Invoke(result.routePoints);
            }
            else
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"SimulationRouteRandomizer: Random route #{requestId} calculation failed");
                }
            }
        }
        catch (System.Exception ex)
        {
            isRandomRouteRequestPending = false;
            
            if (showDebugInfo)
            {
                Debug.LogError($"SimulationRouteRandomizer: Exception during random route calculation: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Cancels any pending random route requests
    /// </summary>
    private void CancelPendingRandomRouteRequest()
    {
        if (isRandomRouteRequestPending)
        {
            currentRandomRouteRequestId++;
            isRandomRouteRequestPending = false;
            
            if (showDebugInfo)
            {
                Debug.Log("SimulationRouteRandomizer: Cancelled pending random route request");
            }
        }
    }
    
    /// <summary>
    /// Track distance for randomization - checks player movement and triggers randomization if needed
    /// </summary>
    private void TrackDistanceForRandomization()
    {
        Vector3 currentPosition = playerTransform.position;
        
        if (lastPlayerPosition != Vector3.zero)
        {
            float distanceThisFrame = Vector3.Distance(lastPlayerPosition, currentPosition);
            totalDistanceTraveled += distanceThisFrame;
            
            if (totalDistanceTraveled >= nextRandomizationDistance)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"SimulationRouteRandomizer: Reached randomization distance {nextRandomizationDistance:F2} (traveled: {totalDistanceTraveled:F2})");
                }
                
                PerformRandomizationRoll();
                
                nextRandomizationDistance = totalDistanceTraveled + randomizationDistanceInterval;
                
                if (showDebugInfo)
                {
                    Debug.Log($"SimulationRouteRandomizer: Next randomization at distance {nextRandomizationDistance:F2}");
                }
            }
        }   
        lastPlayerPosition = currentPosition;
    }
    
    /// <summary>
    /// Performs a randomization roll and generates a random point if successful
    /// </summary>
    private void PerformRandomizationRoll()
    {
        if (!enableRandomization)
            return;
        
        if (hasGeneratedPointForCurrentRoute)
        {
            if (showDebugInfo)
            {
                Debug.Log("SimulationRouteRandomizer: Already generated a point for this route, skipping randomization");
            }
            return;
        }
        
        float randomizationProbability = GetRandomizationProbability();
        
        float randomRoll = Random.Range(0f, 1f);
        
        if (showDebugInfo)
        {
            Debug.Log($"SimulationRouteRandomizer: Randomization roll: {randomRoll:F3}, Threshold: {randomizationProbability:F3}");
        }
        
        // Check if randomization should trigger
        if (randomRoll <= randomizationProbability)
        {
            if (showDebugInfo)
            {
                Debug.Log("SimulationRouteRandomizer: Randomization triggered!");
            }
            
            GenerateRandomPoint();
            hasGeneratedPointForCurrentRoute = true;
        }
        else
        {
            if (showDebugInfo)
            {
                Debug.Log("SimulationRouteRandomizer: Randomization roll failed - no random point generated");
            }
        }
    }
    
    /// <summary>
    /// Starts distance tracking for randomization
    /// </summary>
    private void StartDistanceTracking()
    {
        isTrackingDistance = true;
        lastPlayerPosition = playerTransform.position;
        totalDistanceTraveled = 0f;
        nextRandomizationDistance = randomizationDistanceInterval;
        
        if (showDebugInfo)
        {
            Debug.Log($"SimulationRouteRandomizer: Started distance tracking. First randomization at {nextRandomizationDistance:F2} units");
        }
    }
    
    /// <summary>
    /// Stops distance tracking for randomization
    /// </summary>
    private void StopDistanceTracking()
    {
        isTrackingDistance = false;
        lastPlayerPosition = Vector3.zero;
        totalDistanceTraveled = 0f;
        nextRandomizationDistance = 0f;
        
        if (showDebugInfo)
        {
            Debug.Log("SimulationRouteRandomizer: Stopped distance tracking");
        }
    }
    
    /// <summary>
    /// Sets whether random points are visible (called by SettingsManager)
    /// </summary>
    /// <param name="visible">True to show random points, false to hide them</param>
    public void SetShowRandomPoints(bool visible)
    {
        showRandomPoints = visible;
        
        // Update visibility of existing random points
        foreach (GameObject randomPoint in activeRandomPoints)
        {
            if (randomPoint != null)
            {
                randomPoint.SetActive(visible);
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"SimulationRouteRandomizer: Random points visibility {(visible ? "enabled" : "disabled")} via SettingsManager");
        }
    }
}
