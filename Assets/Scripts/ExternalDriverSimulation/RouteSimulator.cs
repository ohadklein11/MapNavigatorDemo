using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles simulation of driving along a calculated route
/// Moves the player position marker along the route points with smooth interpolation
/// Simplified version without randomization - follows the original route only
/// </summary>
public class RouteSimulator : MonoBehaviour
{
    [Header("Simulation Settings")]
    [SerializeField] private float simulationSpeed = 5f;
    [SerializeField] private float realWorldSpeedKmh = 50f;
    [SerializeField] private float destinationArrivalTolerance = .01f;
    
    [Header("References")]
    [SerializeField] private Transform playerMarker;
    [SerializeField] private RouteManager routeManager;
    [SerializeField] private SimulationRouteRandomizer routeRandomizer;
    
    [Header("Debug Settings")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showCurrentTarget = false;
    [SerializeField] private GameObject targetDebugMarker;
    
    // Internal state
    private List<Vector3> routePoints;
    private int currentPointIndex = 0;
    private bool isSimulating = false;
    private Vector3 simulationStartPosition;
    private Coroutine simulationCoroutine;
    private GameObject currentDebugMarker;
    private bool isRouteRecalculation = false; // Track if the current route is from a recalculation
    
    // Random route handling
    private List<Vector3> pendingRandomRoute = null;
    private bool isFollowingRandomRoute = false;
    private bool hasWarpedToRouteStart = false;
    private bool isStartingFromCurrentPosition = false;
    private Vector3 originalRouteDestination;

    // Events
    public System.Action OnSimulationStarted;
    public System.Action OnSimulationCompleted;
    public System.Action OnSimulationStopped;
    public System.Action<int, int> OnPointReached;
    
    void Start()
    {
        routeManager.OnRouteRecalculated += OnRouteRecalculated;
        routeManager.OnNewRouteAvailable += OnNewRouteAvailable;
        routeManager.OnRouteHidden += OnRouteHidden;
        routeRandomizer.OnRandomRouteReady += OnRandomRouteReady;
        
        if (showDebugInfo)
        {
            Debug.Log("RouteSimulator: Subscribed to events");
        }
    }
    
    /// <summary>
    /// Starts the route simulation with the provided route points
    /// </summary>
    /// <param name="points">List of world positions representing the route</param>
    public void StartSimulation(List<Vector3> points)
    {
        if (points == null || points.Count < 2)
        {
            Debug.LogError("RouteSimulator: Cannot start simulation - need at least 2 route points!");
            return;
        }
        
        if (playerMarker == null)
        {
            Debug.LogError("RouteSimulator: Cannot start simulation - player marker not assigned!");
            return;
        }
        
        if (isSimulating)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("RouteSimulator: Simulation already in progress, stopping current simulation");
            }
            StopSimulation();
        }
        
        // Setup simulation
        routePoints = new List<Vector3>(points);
        simulationStartPosition = playerMarker.position;
        currentPointIndex = 0;
        isSimulating = true;
        isFollowingRandomRoute = false;
        pendingRandomRoute = null;
        isStartingFromCurrentPosition = false;
        
        if (points.Count > 0)
        {
            originalRouteDestination = points[points.Count - 1];
        }
        
        simulationCoroutine = StartCoroutine(SimulateRouteMovement());
        OnSimulationStarted?.Invoke();
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteSimulator: Started simulation with {routePoints.Count} points at speed {simulationSpeed} units/sec");
        }
    }
    
    /// <summary>
    /// Stops the current simulation
    /// </summary>
    public void StopSimulation()
    {
        if (!isSimulating)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("RouteSimulator: No simulation in progress to stop");
            }
            return;
        }
        
        if (simulationCoroutine != null)
        {
            StopCoroutine(simulationCoroutine);
            simulationCoroutine = null;
        }
        
        isSimulating = false;
        currentPointIndex = 0;
        pendingRandomRoute = null;
        isFollowingRandomRoute = false;
        hasWarpedToRouteStart = false;
        isStartingFromCurrentPosition = false;
        
        CleanupDebugMarker();
        
        OnSimulationStopped?.Invoke();
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteSimulator: StopSimulation called from:\n{System.Environment.StackTrace}");
        }
    }
    
    /// <summary>
    /// Main coroutine that handles the route simulation movement
    /// Handles warping to first route start, but not to subsequent routes
    /// </summary>
    IEnumerator SimulateRouteMovement()
    {
        // Only warp to route start for the very first route in the simulation session
        if (routePoints.Count > 0 && !hasWarpedToRouteStart)
        {
            playerMarker.position = routePoints[0];
            hasWarpedToRouteStart = true;
            currentPointIndex = 1;
            
            if (showDebugInfo)
            {
                Debug.Log($"RouteSimulator: Starting route simulation from point 0 of {routePoints.Count} points (warped to start)");
            }
        }
        else if (routePoints.Count > 0)
        {
            // For subsequent routes (recalculated or random), start from current position
            currentPointIndex = 1;

            if (showDebugInfo)
            {
                Debug.Log($"RouteSimulator: Continuing route simulation with {routePoints.Count} points (no warp, starting from current position)");
            }
        }
        
        while (currentPointIndex < routePoints.Count && isSimulating)
        {
            // Safety check for valid route points
            if (routePoints == null || routePoints.Count == 0)
            {
                if (showDebugInfo)
                {
                    Debug.LogError("RouteSimulator: Route points became null or empty during simulation!");
                }
                break;
            }
            
            if (currentPointIndex >= routePoints.Count)
            {
                if (showDebugInfo)
                {
                    Debug.LogError($"RouteSimulator: Current point index {currentPointIndex} exceeds route count {routePoints.Count}!");
                }
                break;
            }
            
            Vector3 startPoint, targetPoint;
            
            // Determine start and target points
            if (isStartingFromCurrentPosition)
            {
                // When starting from current position (e.g., after switching to random route),
                // move directly from current position to the target point
                startPoint = playerMarker.position;
                targetPoint = routePoints[currentPointIndex];
                isStartingFromCurrentPosition = false;
                
                if (showDebugInfo)
                {
                    Debug.Log($"RouteSimulator: Starting from current position {startPoint} to route point {currentPointIndex} {targetPoint}");
                }
            }
            else if (currentPointIndex == 1 && !hasWarpedToRouteStart)
            {
                // For non-first routes, start from current player position to first route point
                startPoint = playerMarker.position;
                targetPoint = routePoints[0];
                
                if (showDebugInfo)
                {
                    Debug.Log($"RouteSimulator: Moving from current position {startPoint} to route point 0 {targetPoint}");
                }
            }
            else
            {
                // Normal movement between consecutive route points
                startPoint = routePoints[currentPointIndex - 1];
                targetPoint = routePoints[currentPointIndex];
            }
            
            UpdateDebugMarker(targetPoint);            
            float distance = Vector3.Distance(startPoint, targetPoint);
            float moveSpeed = CalculateMovementSpeed(distance);
            float journeyTime = distance / moveSpeed;
            
            if (showDebugInfo)
            {
                Debug.Log($"RouteSimulator: Moving from point {currentPointIndex - 1} to {currentPointIndex} " +
                         $"(distance: {distance:F2}, time: {journeyTime:F2}s)");
            }
            
            // Lerp from start to target point
            float elapsedTime = 0f;
            
            while (elapsedTime < journeyTime && isSimulating)
            {
                float progress = elapsedTime / journeyTime;
                Vector3 currentPosition = Vector3.Lerp(startPoint, targetPoint, progress);
                playerMarker.position = currentPosition;
                
                // Check if player accidentally reached original destination while following random route
                if (isFollowingRandomRoute && IsPlayerAtOriginalDestination())
                {
                    if (showDebugInfo)
                    {
                        Debug.Log("RouteSimulator: Player accidentally reached original destination during movement on random route - completing simulation");
                    }
                    CompleteSimulation();
                    yield break;
                }
                
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            // Ensure we end up exactly at the target point
            if (isSimulating)
            {
                playerMarker.position = targetPoint;
                
                // Check if player accidentally reached original destination while following random route
                if (isFollowingRandomRoute && IsPlayerAtOriginalDestination())
                {
                    if (showDebugInfo)
                    {
                        Debug.Log("RouteSimulator: Player accidentally reached original destination while following random route - completing simulation");
                    }
                    CompleteSimulation();
                    yield break;
                }
                
                OnPointReached?.Invoke(currentPointIndex, routePoints.Count - 1);
                
                if (showDebugInfo)
                {
                    Debug.Log($"RouteSimulator: Reached point {currentPointIndex}/{routePoints.Count - 1}");
                }
            }
            currentPointIndex++;
        }
        
        // Simulation completed
        if (isSimulating)
        {
            if (showDebugInfo)
            {
                Debug.Log($"RouteSimulator: Route simulation completed normally - reached end of route with {routePoints.Count} points");
            }
            CompleteSimulation();
        }
        else
        {
            if (showDebugInfo)
            {
                Debug.Log("RouteSimulator: Simulation coroutine ended - isSimulating was false");
            }
        }
    }
    
    /// <summary>
    /// Calculates the movement speed based on settings
    /// </summary>
    float CalculateMovementSpeed(float segmentDistance)
    {
        return simulationSpeed;
    }
    
    /// <summary>
    /// Completes the simulation and performs cleanup
    /// </summary>
    void CompleteSimulation()
    {
        // Normal completion
        isSimulating = false;
        currentPointIndex = 0;
        
        // Reset random route state
        pendingRandomRoute = null;
        isFollowingRandomRoute = false;
        hasWarpedToRouteStart = false; // Reset warp flag for next simulation session
        
        // Clean up debug marker
        CleanupDebugMarker();
        
        // Invoke completion event - let RouteManager handle route cleanup
        OnSimulationCompleted?.Invoke();
        
        if (showDebugInfo)
        {
            Debug.Log("RouteSimulator: Simulation completed!");
        }
    }
    
    /// <summary>
    /// Updates the debug marker position if enabled
    /// </summary>
    void UpdateDebugMarker(Vector3 targetPosition)
    {
        if (!showCurrentTarget || targetDebugMarker == null)
            return;
        
        if (currentDebugMarker == null)
        {
            currentDebugMarker = Instantiate(targetDebugMarker, targetPosition, Quaternion.identity);
            currentDebugMarker.name = "RouteSimulator_TargetDebug";
        }
        else
        {
            currentDebugMarker.transform.position = targetPosition;
        }
    }
    
    /// <summary>
    /// Cleans up the debug marker
    /// </summary>
    void CleanupDebugMarker()
    {
        if (currentDebugMarker != null)
        {
            DestroyImmediate(currentDebugMarker);
            currentDebugMarker = null;
        }
    }
    
    /// <summary>
    /// Returns whether simulation is currently active
    /// </summary>
    public bool IsSimulating()
    {
        return isSimulating;
    }
    
    /// <summary>
    /// Gets the current simulation progress (0-1)
    /// </summary>
    public float GetSimulationProgress()
    {
        if (!isSimulating || routePoints == null || routePoints.Count <= 1)
            return 0f;
        
        return (float)currentPointIndex / (routePoints.Count - 1);
    }
    
    /// <summary>
    /// Gets the current point index in the route
    /// </summary>
    public int GetCurrentPointIndex()
    {
        return currentPointIndex;
    }
    
    /// <summary>
    /// Gets the total number of route points
    /// </summary>
    public int GetTotalPoints()
    {
        return routePoints?.Count ?? 0;
    }
    
    /// <summary>
    /// Sets the simulation speed
    /// </summary>
    public void SetSimulationSpeed(float speed)
    {
        simulationSpeed = Mathf.Max(0.1f, speed);
    }
    
    /// <summary>
    /// Sets the real-world speed in km/h
    /// </summary>
    public void SetRealWorldSpeed(float speedKmh)
    {
        realWorldSpeedKmh = Mathf.Max(1f, speedKmh);
    }
    
    /// <summary>
    /// Event handler for when a new route becomes available from RouteManager
    /// Only automatically starts simulation for recalculations or if user has manually started before
    /// </summary>
    /// <param name="newRoutePoints">List of points for the new route</param>
    private void OnNewRouteAvailable(List<Vector3> newRoutePoints)
    {
        if (newRoutePoints == null || newRoutePoints.Count < 2)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("RouteSimulator: Received invalid route from RouteManager");
            }
            return;
        }
        
        pendingRandomRoute = null;
        originalRouteDestination = newRoutePoints[newRoutePoints.Count - 1];
        bool wasFollowingRandomRoute = isFollowingRandomRoute;
        isFollowingRandomRoute = false;
        routePoints = new List<Vector3>(newRoutePoints);
        bool shouldAutoStart = isRouteRecalculation;
        
        if (showDebugInfo)
        {
            string reason = isRouteRecalculation ? "route recalculation" : "first route - waiting for user input";
            string routeType = wasFollowingRandomRoute ? " (switching from random route)" : "";
            Debug.Log($"RouteSimulator: New route available with {newRoutePoints.Count} points - {reason}{routeType}");
        }
        
        // Stop current simulation if running
        bool wasSimulatingBeforeStop = isSimulating;
        if (isSimulating)
        {
            StopSimulation();
        }
        
        if (shouldAutoStart)
        {
            // For route recalculations during active simulation, process the route to continue from current position
            if (isRouteRecalculation && wasSimulatingBeforeStop)
            {
                Vector3 currentPlayerPosition = playerMarker.position;
                List<Vector3> processedRoute = ProcessRecalculatedRouteForCurrentPosition(newRoutePoints, currentPlayerPosition);
                
                if (processedRoute != null && processedRoute.Count >= 2)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log($"RouteSimulator: Starting recalculated route from current position with {processedRoute.Count} processed points");
                    }
                    StartSimulationFromCurrentPosition(processedRoute);
                }
                else
                {
                    if (showDebugInfo)
                    {
                        Debug.LogWarning("RouteSimulator: Failed to process recalculated route, falling back to standard start");
                    }
                    StartSimulation(newRoutePoints);
                }
            }
            else
            {
                StartSimulation(newRoutePoints);
            }
        }
        else
        {
            // For the first route, wait for user to click "Simulate Drive"
            if (showDebugInfo)
            {
                Debug.Log("RouteSimulator: Route ready - waiting for user to click 'Simulate Drive' button");
            }
        }
        isRouteRecalculation = false;
    }
    
    /// <summary>
    /// Event handler for when the route is hidden by RouteManager
    /// Stops the current simulation
    /// </summary>
    private void OnRouteHidden()
    {
        if (showDebugInfo)
        {
            Debug.Log("RouteSimulator: Route hidden event received - stopping simulation");
        }
        
        if (isSimulating)
        {
            StopSimulation();
        }
    }
    
    /// <summary>
    /// Event handler for when a route is recalculated (e.g., due to course change)
    /// Sets flag to auto-start the next route
    /// NOTE: This event is fired BEFORE OnNewRouteAvailable to ensure the flag is set correctly
    /// </summary>
    private void OnRouteRecalculated()
    {
        isRouteRecalculation = true;
        
        if (showDebugInfo)
        {
            Debug.Log("RouteSimulator: Route recalculation detected - next route will auto-start");
        }
    }
    
    /// <summary>
    /// Event handler for when a random route is ready from SimulationRouteRandomizer
    /// </summary>
    /// <param name="randomRoutePoints">The calculated random route points</param>
    private void OnRandomRouteReady(List<Vector3> randomRoutePoints)
    {
        if (randomRoutePoints == null || randomRoutePoints.Count < 2)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("RouteSimulator: Received invalid random route");
            }
            return;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteSimulator: Random route ready with {randomRoutePoints.Count} points");
        }
        
        pendingRandomRoute = new List<Vector3>(randomRoutePoints);
        
        if (isSimulating)
        {
            SwitchToRandomRoute();
        }
    }
    
    /// <summary>
    /// Switches the current simulation to follow the pending random route
    /// </summary>
    private void SwitchToRandomRoute()
    {
        if (pendingRandomRoute == null || pendingRandomRoute.Count < 2)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("RouteSimulator: Cannot switch to random route - no valid random route pending");
            }
            return;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteSimulator: Switching to random route with {pendingRandomRoute.Count} points");
        }
        
        Vector3 currentPlayerPosition = playerMarker.position;
        
        // Process the random route to start from player's current position
        List<Vector3> processedRoute = ProcessRandomRouteForCurrentPosition(pendingRandomRoute, currentPlayerPosition);
        pendingRandomRoute = null;
        
        if (processedRoute == null || processedRoute.Count < 2)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("RouteSimulator: Processed random route is too short, cannot switch");
            }
            return;
        }
        
        routePoints = processedRoute;
        isFollowingRandomRoute = true;
        isStartingFromCurrentPosition = true;
        
        currentPointIndex = 1;
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteSimulator: Now following processed random route with {routePoints.Count} points, starting from current position");
        }
    }
    
    /// <summary>
    /// Clean up event subscriptions
    /// </summary>
    void OnDestroy()
    {
        if (routeManager != null)
        {
            routeManager.OnNewRouteAvailable -= OnNewRouteAvailable;
            routeManager.OnRouteHidden -= OnRouteHidden;
            routeManager.OnRouteRecalculated -= OnRouteRecalculated;
        }
        
        if (routeRandomizer != null)
        {
            routeRandomizer.OnRandomRouteReady -= OnRandomRouteReady;
        }
        
        if (isSimulating)
        {
            StopSimulation();
        }
    }
    
    /// <summary>
    /// Clean up event subscriptions when disabled
    /// </summary>
    void OnDisable()
    {
        if (isSimulating)
        {
            StopSimulation();
        }
    }
    
    /// <summary>
    /// Manually starts simulation with the currently stored route (called when user clicks "Simulate Drive")
    /// </summary>
    public void StartSimulationManually()
    {
        if (routePoints == null || routePoints.Count < 2)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("RouteSimulator: No route available to simulate");
            }
            return;
        }
        
        if (showDebugInfo)
        {
            Debug.Log("RouteSimulator: Manual simulation start requested by user");
        }
        
        StartSimulation(routePoints);
    }
    
    /// <summary>
    /// Checks if the player is close enough to the original route destination to be considered "arrived"
    /// </summary>
    /// <returns>True if player is at the original destination</returns>
    private bool IsPlayerAtOriginalDestination()
    {
        if (playerMarker == null)
            return false;
        
        float distanceToOriginalDestination = Vector3.Distance(playerMarker.position, originalRouteDestination);
        
        bool isAtDestination = distanceToOriginalDestination <= destinationArrivalTolerance;
        
        if (showDebugInfo && isAtDestination)
        {
            Debug.Log($"RouteSimulator: Player is {distanceToOriginalDestination:F2} units from original destination (tolerance: {destinationArrivalTolerance})");
        }
        
        return isAtDestination;
    }

    /// <summary>
    /// Processes a random route to start from the player's current position
    /// Finds the closest point behind the player and removes it and all previous points
    /// </summary>
    /// <param name="randomRoute">The original random route points</param>
    /// <param name="currentPlayerPosition">Player's current position</param>
    /// <returns>Processed route starting from current position</returns>
    private List<Vector3> ProcessRandomRouteForCurrentPosition(List<Vector3> randomRoute, Vector3 currentPlayerPosition)
    {
        if (randomRoute == null || randomRoute.Count < 2)
        {
            return null;
        }
        
        Vector3 playerDirection = GetPlayerMovementDirection();        
        int startIndex = FindOptimalStartPointIndex(randomRoute, currentPlayerPosition, playerDirection);
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteSimulator: Found optimal start index {startIndex} for random route with {randomRoute.Count} points");
        }
        
        List<Vector3> processedRoute = new List<Vector3>();
        processedRoute.Add(currentPlayerPosition);
        
        for (int i = startIndex; i < randomRoute.Count; i++)
        {
            processedRoute.Add(randomRoute[i]);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteSimulator: Processed random route from {randomRoute.Count} to {processedRoute.Count} points, starting at player position {currentPlayerPosition}");
        }
        
        return processedRoute;
    }
    
    /// <summary>
    /// Processes a recalculated route to start from the player's current position
    /// Similar to ProcessRandomRouteForCurrentPosition but for official route recalculations
    /// </summary>
    /// <param name="recalculatedRoute">The new recalculated route points</param>
    /// <param name="currentPlayerPosition">Player's current position</param>
    /// <returns>Processed route starting from current position</returns>
    private List<Vector3> ProcessRecalculatedRouteForCurrentPosition(List<Vector3> recalculatedRoute, Vector3 currentPlayerPosition)
    {
        if (recalculatedRoute == null || recalculatedRoute.Count < 2)
        {
            return null;
        }
        
        Vector3 playerDirection = GetPlayerMovementDirection();
        int startIndex = FindOptimalStartPointIndex(recalculatedRoute, currentPlayerPosition, playerDirection);
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteSimulator: Found optimal start index {startIndex} for recalculated route with {recalculatedRoute.Count} points");
        }
        
        List<Vector3> processedRoute = new List<Vector3>();
        processedRoute.Add(currentPlayerPosition);
        
        for (int i = startIndex; i < recalculatedRoute.Count; i++)
        {
            processedRoute.Add(recalculatedRoute[i]);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteSimulator: Processed recalculated route from {recalculatedRoute.Count} to {processedRoute.Count} points, starting at player position {currentPlayerPosition}");
        }
        
        return processedRoute;
    }
    
    /// <summary>
    /// Starts simulation from the current player position with a processed route
    /// Used for route recalculations and random routes
    /// </summary>
    /// <param name="processedRoute">Route that starts from current player position</param>
    private void StartSimulationFromCurrentPosition(List<Vector3> processedRoute)
    {
        if (processedRoute == null || processedRoute.Count < 2)
        {
            Debug.LogError("RouteSimulator: Cannot start simulation from current position - need at least 2 route points!");
            return;
        }
        
        if (playerMarker == null)
        {
            Debug.LogError("RouteSimulator: Cannot start simulation - player marker not assigned!");
            return;
        }
        
        if (isSimulating)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("RouteSimulator: Simulation already in progress, stopping current simulation");
            }
            StopSimulation();
        }
        
        // Setup simulation
        routePoints = new List<Vector3>(processedRoute);
        simulationStartPosition = playerMarker.position;
        currentPointIndex = 1;
        isSimulating = true;

        isFollowingRandomRoute = false;
        pendingRandomRoute = null;
        isStartingFromCurrentPosition = true;

        if (processedRoute.Count > 0)
        {
            originalRouteDestination = processedRoute[processedRoute.Count - 1];
        }
        
        simulationCoroutine = StartCoroutine(SimulateRouteMovement());
        OnSimulationStarted?.Invoke();
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteSimulator: Started simulation from current position with {routePoints.Count} points at speed {simulationSpeed} units/sec");
        }
    }
    
    /// <summary>
    /// Calculates the player's current movement direction based on recent movement
    /// </summary>
    /// <returns>Normalized direction vector, or Vector3.zero if no clear direction</returns>
    private Vector3 GetPlayerMovementDirection()
    {
        // If we have route points and are currently moving, use the direction from last point to current point
        if (routePoints != null && currentPointIndex > 0 && currentPointIndex <= routePoints.Count)
        {
            Vector3 lastPoint = routePoints[currentPointIndex - 1];
            Vector3 currentPos = playerMarker.position;
            Vector3 direction = (currentPos - lastPoint).normalized;
            
            if (direction.magnitude > 0.1f)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"RouteSimulator: Player movement direction from route: {direction}");
                }
                return direction;
            }
        }
        
        // Fallback: no clear direction
        if (showDebugInfo)
        {
            Debug.Log("RouteSimulator: No clear player movement direction found");
        }
        return Vector3.zero;
    }
    
    /// <summary>
    /// Finds the optimal starting point index in the random route
    /// Looks for the point that's closest to being behind the player in their movement direction
    /// </summary>
    /// <param name="randomRoute">The random route points</param>
    /// <param name="currentPlayerPosition">Player's current position</param>
    /// <param name="playerDirection">Player's movement direction</param>
    /// <returns>Index of the first point to keep in the route</returns>
    private int FindOptimalStartPointIndex(List<Vector3> randomRoute, Vector3 currentPlayerPosition, Vector3 playerDirection)
    {
        if (randomRoute == null || randomRoute.Count == 0)
        {
            return 0;
        }
        
        float bestScore = float.MinValue;
        int bestIndex = 0;
        
        // If we don't have a clear player direction, just find the closest point
        if (playerDirection.magnitude < 0.1f)
        {
            float closestDistance = float.MaxValue;
            for (int i = 0; i < randomRoute.Count; i++)
            {
                float distance = Vector3.Distance(currentPlayerPosition, randomRoute[i]);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestIndex = i;
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"RouteSimulator: No movement direction, using closest point at index {bestIndex} (distance: {closestDistance:F2})");
            }
            
            return bestIndex;
        }
        
        // Find the point that's most aligned with being "behind" the player
        for (int i = 0; i < randomRoute.Count; i++)
        {
            Vector3 toPoint = (randomRoute[i] - currentPlayerPosition).normalized;
            
            float directionAlignment = Vector3.Dot(-playerDirection, toPoint);
            float distance = Vector3.Distance(currentPlayerPosition, randomRoute[i]);
            float distanceScore = 1.0f / (1.0f + distance * 0.1f); // Inverse distance with scaling
            float totalScore = directionAlignment + distanceScore * 0.5f;
            
            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestIndex = i;
            }
            
            if (showDebugInfo && i < 5)
            {
                Debug.Log($"RouteSimulator: Point {i}: alignment={directionAlignment:F3}, distance={distance:F2}, score={totalScore:F3}");
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"RouteSimulator: Best start index {bestIndex} with score {bestScore:F3}");
        }
        
        return bestIndex;
    }
}
