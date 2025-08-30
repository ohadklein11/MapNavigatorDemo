using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Manages application settings including camera POV, debug visibility, and simulation parameters.
/// Works with a GenericPopup-based settings popup for UI interaction.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GenericPopup settingsPopup; // Reference to the settings popup
    [SerializeField] private Toggle povToggle; // Toggle for POV switching
    [SerializeField] private Toggle courseChangePopupToggle; // Toggle for course change popup
    [SerializeField] private Toggle debugPointsToggle; // Toggle for debug points visibility
    
    [Header("Component References")]
    [SerializeField] private CameraDragController cameraController; // Reference to the camera controller
    [SerializeField] private RouteManager routeManager; // Reference to the route manager
    [SerializeField] private SimulationRouteRandomizer routeRandomizer; // Reference to the route randomizer
    [SerializeField] private RouteSimulator routeSimulator; // Reference to the route simulator
    
    [Header("Player Following Settings")]
    [SerializeField] private Transform playerTransform; // The player/MyPos object to follow
    [SerializeField] private float followSmoothness = 5f; // Smoothness of camera following
    [SerializeField] private float rotationSmoothness = 3f; // Smoothness of camera rotation
    
    [Header("Settings")]
    [SerializeField] private bool showDebugInfo = true;
    
    [Header("Default Setting Values")]
    [SerializeField] private bool defaultPlayerPOV = false; // Default POV mode (false = normal, true = player-focused)
    [SerializeField] private bool defaultShowCourseChangePopup = true; // Default state for course change popup
    [SerializeField] private bool defaultShowDebugPoints = true; // Default state for debug points visibility
    
    // Current settings state
    private bool isPlayerPOV = false; // Current POV mode (false = normal, true = player-focused)
    private bool showCourseChangePopup = true; // Whether to show course change popup
    private bool showDebugPoints = true; // Whether to show debug points
    
    // Camera follow state
    private Camera mainCamera;
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private Vector3 lastPlayerPosition;
    private bool isFollowingPlayer = false;
    private Coroutine followCoroutine;
    
    // Events
    public System.Action<bool> OnPOVChanged; // Event when POV mode changes
    public System.Action<bool> OnCourseChangePopupToggled; // Event when course change popup setting changes
    public System.Action<bool> OnDebugPointsToggled; // Event when debug points visibility changes
    
    void Awake()
    {
        // Find main camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
                mainCamera = FindFirstObjectByType<Camera>();
        }
        
        // Find camera controller if not assigned
        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<CameraDragController>();
        }
        
        // Find route manager if not assigned
        if (routeManager == null)
        {
            routeManager = FindFirstObjectByType<RouteManager>();
        }
        
        // Find route randomizer if not assigned
        if (routeRandomizer == null)
        {
            routeRandomizer = FindFirstObjectByType<SimulationRouteRandomizer>();
        }
        
        // Find route simulator if not assigned
        if (routeSimulator == null)
        {
            routeSimulator = FindFirstObjectByType<RouteSimulator>();
        }
        
        // Find player transform if not assigned
        if (playerTransform == null)
        {
            MapTileGetter mapTileGetter = FindFirstObjectByType<MapTileGetter>();
            if (mapTileGetter != null)
            {
                playerTransform = mapTileGetter.MyPosObject;
            }
        }
        
        // Store original camera settings
        if (mainCamera != null)
        {
            originalCameraPosition = mainCamera.transform.position;
            originalCameraRotation = mainCamera.transform.rotation;
        }
        
        if (showDebugInfo)
        {
            Debug.Log("SettingsManager: Initialized with all component references");
        }
    }
    
    void Start()
    {
        // Initialize settings to default values
        InitializeSettings();
        
        // Setup UI event listeners
        SetupUIListeners();
        
        // Setup simulation event listeners
        SetupSimulationEventListeners();
        
        // Update UI to reflect current settings
        UpdateUI();
        
        if (showDebugInfo)
        {
            Debug.Log("SettingsManager: Started and ready");
        }
    }
    
    void OnDestroy()
    {
        // Clean up UI listeners
        CleanupUIListeners();
        
        // Clean up simulation event listeners
        CleanupSimulationEventListeners();
        
        // Stop following if active
        if (isFollowingPlayer)
        {
            StopFollowingPlayer();
        }
    }
    
    /// <summary>
    /// Initializes settings to their default values
    /// </summary>
    void InitializeSettings()
    {
        // Initialize POV to default value (but don't apply yet since simulation may not be active)
        isPlayerPOV = defaultPlayerPOV;
        
        // Initialize course change popup to default value
        showCourseChangePopup = defaultShowCourseChangePopup;
        
        // Initialize debug points to default value
        showDebugPoints = defaultShowDebugPoints;
        
        // Apply non-POV settings immediately
        ApplyCourseChangePopupSetting(showCourseChangePopup);
        ApplyDebugPointsSetting(showDebugPoints);
        
        // POV setting will be applied when user toggles it or when simulation starts
    }
    
    /// <summary>
    /// Sets up UI event listeners
    /// </summary>
    void SetupUIListeners()
    {
        if (povToggle != null)
        {
            povToggle.onValueChanged.AddListener(OnPOVToggleChanged);
        }
        
        if (courseChangePopupToggle != null)
        {
            courseChangePopupToggle.onValueChanged.AddListener(OnCourseChangePopupToggleChanged);
        }
        
        if (debugPointsToggle != null)
        {
            debugPointsToggle.onValueChanged.AddListener(OnDebugPointsToggleChanged);
        }
        
        if (settingsPopup != null)
        {
            settingsPopup.OnPopupShown += OnSettingsPopupShown;
            settingsPopup.OnPopupHidden += OnSettingsPopupHidden;
        }
    }
    
    /// <summary>
    /// Cleans up UI event listeners
    /// </summary>
    void CleanupUIListeners()
    {
        if (povToggle != null)
        {
            povToggle.onValueChanged.RemoveAllListeners();
        }
        
        if (courseChangePopupToggle != null)
        {
            courseChangePopupToggle.onValueChanged.RemoveAllListeners();
        }
        
        if (debugPointsToggle != null)
        {
            debugPointsToggle.onValueChanged.RemoveAllListeners();
        }
        
        if (settingsPopup != null)
        {
            settingsPopup.OnPopupShown -= OnSettingsPopupShown;
            settingsPopup.OnPopupHidden -= OnSettingsPopupHidden;
        }
    }
    
    /// <summary>
    /// Sets up simulation event listeners
    /// </summary>
    void SetupSimulationEventListeners()
    {
        if (routeSimulator != null)
        {
            routeSimulator.OnSimulationStarted += OnSimulationStarted;
            routeSimulator.OnSimulationStopped += OnSimulationStopped;
            routeSimulator.OnSimulationCompleted += OnSimulationCompleted;
        }
    }
    
    /// <summary>
    /// Cleans up simulation event listeners
    /// </summary>
    void CleanupSimulationEventListeners()
    {
        if (routeSimulator != null)
        {
            routeSimulator.OnSimulationStarted -= OnSimulationStarted;
            routeSimulator.OnSimulationStopped -= OnSimulationStopped;
            routeSimulator.OnSimulationCompleted -= OnSimulationCompleted;
        }
    }
    
    /// <summary>
    /// Updates UI elements to reflect current settings
    /// </summary>
    void UpdateUI()
    {
        if (povToggle != null)
        {
            povToggle.SetIsOnWithoutNotify(isPlayerPOV);
        }
        
        if (courseChangePopupToggle != null)
        {
            courseChangePopupToggle.SetIsOnWithoutNotify(showCourseChangePopup);
        }
        
        if (debugPointsToggle != null)
        {
            debugPointsToggle.SetIsOnWithoutNotify(showDebugPoints);
        }
    }
    
    #region UI Event Handlers
    
    /// <summary>
    /// Called when POV toggle is changed
    /// </summary>
    void OnPOVToggleChanged(bool value)
    {
        SetPOVMode(value);
    }
    
    /// <summary>
    /// Called when course change popup toggle is changed
    /// </summary>
    void OnCourseChangePopupToggleChanged(bool value)
    {
        SetCourseChangePopupEnabled(value);
    }
    
    /// <summary>
    /// Called when debug points toggle is changed
    /// </summary>
    void OnDebugPointsToggleChanged(bool value)
    {
        SetDebugPointsVisible(value);
    }
    
    /// <summary>
    /// Called when settings popup is shown
    /// </summary>
    void OnSettingsPopupShown()
    {
        if (showDebugInfo)
        {
            Debug.Log("SettingsManager: Settings popup shown");
        }
        
        // Update UI to reflect current settings when popup opens
        UpdateUI();
    }
    
    /// <summary>
    /// Called when settings popup is hidden
    /// </summary>
    void OnSettingsPopupHidden()
    {
        if (showDebugInfo)
        {
            Debug.Log("SettingsManager: Settings popup hidden");
        }
    }
    
    /// <summary>
    /// Called when simulation starts - start following if player POV is enabled
    /// </summary>
    void OnSimulationStarted()
    {
        if (isPlayerPOV && !isFollowingPlayer)
        {
            if (showDebugInfo)
            {
                Debug.Log("SettingsManager: Simulation started and Player POV is enabled - starting camera follow");
            }
            StartFollowingPlayer();
        }
    }
    
    /// <summary>
    /// Called when simulation stops - stop following but keep POV setting for next simulation
    /// </summary>
    void OnSimulationStopped()
    {
        if (isFollowingPlayer)
        {
            if (showDebugInfo)
            {
                Debug.Log("SettingsManager: Simulation stopped - stopping camera follow but keeping POV setting");
            }
            StopFollowingPlayer();
        }
    }
    
    /// <summary>
    /// Called when simulation completes - stop following but keep POV setting for next simulation
    /// </summary>
    void OnSimulationCompleted()
    {
        if (isFollowingPlayer)
        {
            if (showDebugInfo)
            {
                Debug.Log("SettingsManager: Simulation completed - stopping camera follow but keeping POV setting");
            }
            StopFollowingPlayer();
        }
    }
    
    #endregion
    
    #region Public Settings API
    
    /// <summary>
    /// Sets the POV mode (normal or player-focused)
    /// </summary>
    /// <param name="playerPOV">True for player-focused POV, false for normal POV</param>
    public void SetPOVMode(bool playerPOV)
    {
        if (showDebugInfo)
        {
            Debug.Log($"SettingsManager: SetPOVMode called with {playerPOV}, current isPlayerPOV: {isPlayerPOV}");
        }
        
        if (isPlayerPOV == playerPOV) return;
        
        // Update internal state first
        isPlayerPOV = playerPOV;
        
        // If trying to enable player POV, check if simulation is active
        if (playerPOV && (routeSimulator == null || !routeSimulator.IsSimulating()))
        {
            if (showDebugInfo)
            {
                Debug.Log("SettingsManager: Player POV enabled but simulation is not active. Will start following when simulation begins.");
            }
            
            // Don't start following yet, but keep the setting enabled
            // The following will start automatically when simulation begins
        }
        else
        {
            // Apply the setting immediately (either disabling POV or enabling when simulation is active)
            ApplyPOVSetting(playerPOV);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"SettingsManager: POV mode changed to {(playerPOV ? "Player-Focused" : "Normal")}");
        }
        
        OnPOVChanged?.Invoke(playerPOV);
    }
    
    /// <summary>
    /// Sets whether course change popup is enabled
    /// </summary>
    /// <param name="enabled">True to show course change popup, false to disable it</param>
    public void SetCourseChangePopupEnabled(bool enabled)
    {
        if (showCourseChangePopup == enabled) return;
        
        showCourseChangePopup = enabled;
        ApplyCourseChangePopupSetting(enabled);
        
        if (showDebugInfo)
        {
            Debug.Log($"SettingsManager: Course change popup {(enabled ? "enabled" : "disabled")}");
        }
        
        OnCourseChangePopupToggled?.Invoke(enabled);
    }
    
    /// <summary>
    /// Sets whether debug points are visible
    /// </summary>
    /// <param name="visible">True to show debug points, false to hide them</param>
    public void SetDebugPointsVisible(bool visible)
    {
        if (showDebugPoints == visible) return;
        
        showDebugPoints = visible;
        ApplyDebugPointsSetting(visible);
        
        if (showDebugInfo)
        {
            Debug.Log($"SettingsManager: Debug points {(visible ? "enabled" : "disabled")}");
        }
        
        OnDebugPointsToggled?.Invoke(visible);
    }
    
    /// <summary>
    /// Shows the settings popup
    /// </summary>
    public void ShowSettings()
    {
        if (settingsPopup != null)
        {
            settingsPopup.ShowPopup();
        }
    }
    
    /// <summary>
    /// Hides the settings popup
    /// </summary>
    public void HideSettings()
    {
        if (settingsPopup != null)
        {
            settingsPopup.HidePopup();
        }
    }
    
    /// <summary>
    /// Toggles the settings popup visibility
    /// </summary>
    public void ToggleSettings()
    {
        if (settingsPopup != null)
        {
            settingsPopup.TogglePopup();
        }
    }
    
    #endregion
    
    #region Settings Application
    
    /// <summary>
    /// Applies the POV setting to the camera system
    /// </summary>
    void ApplyPOVSetting(bool playerPOV)
    {
        if (playerPOV)
        {
            StartFollowingPlayer();
        }
        else
        {
            StopFollowingPlayer();
        }
    }
    
    /// <summary>
    /// Applies the course change popup setting to the route manager
    /// </summary>
    void ApplyCourseChangePopupSetting(bool enabled)
    {
        if (routeManager != null)
        {
            routeManager.SetCourseChangePopupEnabled(enabled);
        }
    }
    
    /// <summary>
    /// Applies the debug points setting to the route manager and randomizer
    /// </summary>
    void ApplyDebugPointsSetting(bool visible)
    {
        if (routeManager != null)
        {
            routeManager.SetShowRouteSegmentPoints(visible);
            routeManager.SetShowPlayerHistoryPoints(visible);
        }
        
        if (routeRandomizer != null)
        {
            routeRandomizer.SetShowRandomPoints(visible);
        }
    }
    
    #endregion
    
    #region Player Following
    
    /// <summary>
    /// Starts following the player with the camera
    /// </summary>
    void StartFollowingPlayer()
    {
        if (showDebugInfo)
        {
            Debug.Log($"SettingsManager: StartFollowingPlayer called - isFollowingPlayer: {isFollowingPlayer}, playerTransform: {playerTransform != null}, mainCamera: {mainCamera != null}");
            if (routeSimulator != null)
            {
                Debug.Log($"SettingsManager: RouteSimulator found, IsSimulating: {routeSimulator.IsSimulating()}");
            }
            else
            {
                Debug.Log("SettingsManager: RouteSimulator is null");
            }
        }
        
        if (isFollowingPlayer || playerTransform == null || mainCamera == null) return;
        
        // Only start following if simulation is currently active
        if (routeSimulator == null || !routeSimulator.IsSimulating())
        {
            if (showDebugInfo)
            {
                Debug.Log("SettingsManager: Cannot start following player - simulation is not active");
            }
            return;
        }
        
        isFollowingPlayer = true;
        
        // Disable camera dragging
        if (cameraController != null)
        {
            cameraController.SetDraggingEnabled(false);
        }
        
        // Start follow coroutine
        if (followCoroutine != null)
        {
            StopCoroutine(followCoroutine);
        }
        followCoroutine = StartCoroutine(FollowPlayerCoroutine());
        
        if (showDebugInfo)
        {
            Debug.Log("SettingsManager: Started following player");
        }
    }
    
    /// <summary>
    /// Stops following the player and returns to normal camera mode
    /// </summary>
    void StopFollowingPlayer()
    {
        if (!isFollowingPlayer) return;
        
        isFollowingPlayer = false;
        
        // Stop follow coroutine
        if (followCoroutine != null)
        {
            StopCoroutine(followCoroutine);
            followCoroutine = null;
        }
        
        // Re-enable camera dragging
        if (cameraController != null)
        {
            cameraController.SetDraggingEnabled(true);
        }
        
        // Reset camera rotation to original (0,0,0)
        if (mainCamera != null)
        {
            mainCamera.transform.rotation = originalCameraRotation;
        }
        
        if (showDebugInfo)
        {
            Debug.Log("SettingsManager: Stopped following player and reset camera rotation");
        }
    }
    
    /// <summary>
    /// Coroutine that handles smooth camera following of the player
    /// </summary>
    IEnumerator FollowPlayerCoroutine()
    {
        if (playerTransform == null || mainCamera == null) yield break;
        
        lastPlayerPosition = playerTransform.position;
        
        while (isFollowingPlayer)
        {
            if (playerTransform == null || mainCamera == null) yield break;
            
            // Check if simulation is still active - stop following if not
            if (routeSimulator == null || !routeSimulator.IsSimulating())
            {
                if (showDebugInfo)
                {
                    Debug.Log("SettingsManager: Simulation stopped during following - stopping camera follow");
                }
                SetPOVMode(false); // This will stop following and update UI
                yield break;
            }
            
            Vector3 currentPlayerPosition = playerTransform.position;
            
            // Update camera position to follow player
            Vector3 targetCameraPosition = new Vector3(currentPlayerPosition.x, currentPlayerPosition.y, mainCamera.transform.position.z);
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetCameraPosition, followSmoothness * Time.deltaTime);
            
            // Calculate player movement direction for camera rotation
            Vector3 movementDirection = currentPlayerPosition - lastPlayerPosition;
            if (movementDirection.magnitude > 0.0001f) // Only rotate if player is moving meaningfully
            {
                // Calculate rotation to align camera with movement direction
                float targetAngle = Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg - 90f; // -90 to point "up" in movement direction
                Quaternion targetRotation = Quaternion.AngleAxis(targetAngle, Vector3.forward);
                
                // Smoothly rotate camera
                mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, targetRotation, rotationSmoothness * Time.deltaTime);
            }
            
            lastPlayerPosition = currentPlayerPosition;
            yield return null;
        }
    }
    
    #endregion
    
    #region Getters
    
    /// <summary>
    /// Gets the current POV mode
    /// </summary>
    /// <returns>True if in player-focused POV, false if in normal POV</returns>
    public bool IsPlayerPOV()
    {
        return isPlayerPOV;
    }
    
    /// <summary>
    /// Gets whether course change popup is enabled
    /// </summary>
    /// <returns>True if course change popup is enabled</returns>
    public bool IsCourseChangePopupEnabled()
    {
        return showCourseChangePopup;
    }
    
    /// <summary>
    /// Gets whether debug points are visible
    /// </summary>
    /// <returns>True if debug points are visible</returns>
    public bool AreDebugPointsVisible()
    {
        return showDebugPoints;
    }
    
    /// <summary>
    /// Gets whether the camera is currently following the player
    /// </summary>
    /// <returns>True if camera is following player</returns>
    public bool IsFollowingPlayer()
    {
        return isFollowingPlayer;
    }
    
    #endregion
    
    #region Public Convenience Methods (for UI integration)
    
    /// <summary>
    /// Toggles the POV mode between normal and player-focused
    /// </summary>
    public void TogglePOVMode()
    {
        SetPOVMode(!isPlayerPOV);
    }
    
    /// <summary>
    /// Toggles the course change popup enabled state
    /// </summary>
    public void ToggleCourseChangePopup()
    {
        SetCourseChangePopupEnabled(!showCourseChangePopup);
    }
    
    /// <summary>
    /// Toggles the debug points visibility
    /// </summary>
    public void ToggleDebugPoints()
    {
        SetDebugPointsVisible(!showDebugPoints);
    }
    
    /// <summary>
    /// Resets all settings to their default values
    /// </summary>
    public void ResetToDefaults()
    {
        SetPOVMode(defaultPlayerPOV);
        SetCourseChangePopupEnabled(defaultShowCourseChangePopup);
        SetDebugPointsVisible(defaultShowDebugPoints);
        
        if (showDebugInfo)
        {
            Debug.Log("SettingsManager: All settings reset to defaults");
        }
    }
    
    /// <summary>
    /// Gets a summary string of current settings (useful for debugging)
    /// </summary>
    /// <returns>String summary of current settings</returns>
    public string GetSettingsSummary()
    {
        return $"POV: {(isPlayerPOV ? "Player-Focused" : "Normal")}, " +
               $"Course Change Popup: {(showCourseChangePopup ? "Enabled" : "Disabled")}, " +
               $"Debug Points: {(showDebugPoints ? "Visible" : "Hidden")}";
    }
    
    #endregion
}
