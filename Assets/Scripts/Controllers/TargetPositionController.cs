using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Controls the TgtPos target position marker.
/// Handles double-click to set target position at mouse location.
/// </summary>
public class TargetPositionController : MonoBehaviour
{
    [Header("Target Position Settings")]
    [SerializeField] private GameObject tgtPosObject;
    [SerializeField] private Image tgtPosUIImage;
    [SerializeField] private float doubleClickTime = 0.3f;
    [SerializeField] private float maxDragDistance = 10f;
    
    [Header("Popup UI")]
    [SerializeField] private GenericPopup drivePopup;
    [SerializeField] private TMPro.TextMeshProUGUI locationText;

    [Header("Route Navigation")]
    [SerializeField] private RouteManager routeManager;
    [SerializeField] private MapTileGetter mapTileGetter;

    [Header("Visual Feedback")]
    [SerializeField] private bool showDebugInfo = true;
    
    private Camera mainCamera;
    private float lastClickTime;
    private int clickCount;
    private bool targetIsActive = false;
    private Vector3 mouseDownPosition;
    
    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindFirstObjectByType<Camera>();
        
        DisableTarget();
        
        if (drivePopup != null)
        {
            drivePopup.OnActionButtonClicked += OnDrivePopupActionClicked;
        }
                
        if (showDebugInfo)
        {
            Debug.Log("TargetPositionController initialized. Double-click to place target.");
        }
    }
    
    void OnDestroy()
    {
        if (drivePopup != null)
        {
            drivePopup.OnActionButtonClicked -= OnDrivePopupActionClicked;
        }
    }
    
    void Update()
    {
        HandleDoubleClick();
    }
    
    void HandleDoubleClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            mouseDownPosition = Input.mousePosition;
        }
        
        if (Input.GetMouseButtonUp(0))
        {
            float dragDistance = Vector3.Distance(Input.mousePosition, mouseDownPosition);
            
            if (dragDistance <= maxDragDistance)
            {
                float timeSinceLastClick = Time.time - lastClickTime;
                
                if (timeSinceLastClick <= doubleClickTime)
                {
                    clickCount++;
                    
                    if (clickCount >= 2)
                    {
                        OnDoubleClick();
                        clickCount = 0;
                    }
                }
                else
                {
                    clickCount = 1;
                }
                
                lastClickTime = Time.time;
            }
            else
            {
                clickCount = 0;
            }
        }
    }
    
    void OnDoubleClick()
    {
        if (mainCamera == null)
        {
            Debug.LogWarning("TargetPositionController: No camera found!");
            return;
        }
        
        Vector3 mouseScreenPos = Input.mousePosition;
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0));
        worldPos.z = 0;
        SetTargetPosition(worldPos);
        
        if (showDebugInfo)
        {
            Vector2 gpsCoords = GetTargetGPSCoordinates();
            Debug.Log($"Target position set to: World({worldPos.x:F2}, {worldPos.y:F2}) GPS({gpsCoords.x:F6}, {gpsCoords.y:F6})");
        }
    }
    
    /// <summary>
    /// Sets the target position and enables the target marker
    /// </summary>
    /// <param name="worldPosition">World position to place the target</param>
    public void SetTargetPosition(Vector3 worldPosition)
    {
        if (routeManager.HasActiveRoute())
        {
            routeManager.HideRoute();
            if (showDebugInfo)
            {
                Debug.Log("Previous route hidden due to new target placement");
            }
        }
        tgtPosUIImage.gameObject.SetActive(false);
        tgtPosObject.transform.position = worldPosition;
        tgtPosObject.SetActive(true);
        targetIsActive = true;
        
        StartCoroutine(EnableUIAfterFrame());
    }
    
    /// <summary>
    /// Enables the UI after waiting one frame for position updates
    /// </summary>
    private System.Collections.IEnumerator EnableUIAfterFrame()
    {
        yield return null;
        
        tgtPosUIImage.gameObject.SetActive(true);
        
        if (showDebugInfo)
        {
            Debug.Log("Target marker enabled");
        }
        
        ShowTargetPopup();
    }
    
    /// <summary>
    /// Shows the popup UI for the target using GenericPopup
    /// </summary>
    private void ShowTargetPopup()
    {
        if (drivePopup != null)
        {
            Vector2 gpsCoords = GetTargetGPSCoordinates();
            
            if (locationText != null)
            {
                locationText.text = $"Lat: {gpsCoords.x:F6}\nLon: {gpsCoords.y:F6}";
            }            
            drivePopup.ShowPopupAtWorldPosition(tgtPosObject.transform);
    
            if (showDebugInfo)
            {
                Debug.Log($"Drive popup shown for GPS coordinates: {gpsCoords.x:F6}, {gpsCoords.y:F6}");
            }
        }
    }
    
    /// <summary>
    /// Called when an action button in the DrivePopup is clicked (Drive Here button)
    /// </summary>
    private void OnDrivePopupActionClicked(int buttonIndex)
    {
        // The only action button is the "Drive Here" button
        if (buttonIndex == 0)
        {
            if (showDebugInfo)
            {
                Vector2 gpsCoords = GetTargetGPSCoordinates();
                Debug.Log($"Drive here clicked for GPS coordinates: {gpsCoords.x:F6}, {gpsCoords.y:F6}");
            }
            
            // Calculate and display route
            Transform myPosObject = mapTileGetter.MyPosObject;
            if (myPosObject != null)
            {
                routeManager.CalculateRoute(myPosObject, tgtPosObject.transform, (success) => {
                    if (success)
                    {
                        if (showDebugInfo)
                        {
                            Debug.Log("Route calculated successfully!");
                        }
                    }
                    else
                    {
                        Debug.LogError("Failed to calculate route!");
                    }
                });
            }
            else
            {
                Debug.LogWarning("Target position not set!");
            }
        }
    }

    /// <summary>
    /// Disables the target position marker and its UI image
    /// </summary>
    public void DisableTarget()
    {
        tgtPosObject.SetActive(false);
        targetIsActive = false;
        tgtPosUIImage.gameObject.SetActive(false);
        drivePopup.HidePopup();
        routeManager.HideRoute();
        
        if (showDebugInfo)
        {
            Debug.Log("Target marker disabled");
        }
    }
    
    /// <summary>
    /// Hides the target marker without affecting routes or popups
    /// Used when simulation completes and we want to clean up the target marker
    /// </summary>
    public void HideTargetMarker()
    {
        tgtPosObject.SetActive(false);
        targetIsActive = false;
        tgtPosUIImage.gameObject.SetActive(false);
        
        if (showDebugInfo)
        {
            Debug.Log("Target marker hidden after simulation completion");
        }
    }

    /// <summary>
    /// Gets the current target position (world coordinates)
    /// </summary>
    public Vector3 GetTargetPosition()
    {
        return tgtPosObject.transform.position;
    }
    
    /// <summary>
    /// Converts the target position to GPS coordinates using the MapTileGetter's coordinate system
    /// </summary>
    public Vector2 GetTargetGPSCoordinates()
    {
        if (!targetIsActive)
        {
            return Vector2.zero;
        }
        
        Vector3 worldPos = GetTargetPosition();
        return mapTileGetter.WorldToGPSCoordinates(worldPos);
    }
}
