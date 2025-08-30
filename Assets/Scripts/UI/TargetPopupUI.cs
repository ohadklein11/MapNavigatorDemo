using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// Popup window that appears when a target position is set
/// Contains "Drive here" and "X" buttons
/// </summary>
public class TargetPopupUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject popupPanel; // The main popup panel
    [SerializeField] private Button driveHereButton; // "Drive here" button
    [SerializeField] private Button closeButton; // "X" button
    [SerializeField] private TextMeshProUGUI locationText; // Optional TextMeshPro text to show GPS coordinates
    
    [Header("Animation Settings")]
    [SerializeField] private bool useAnimation = true;
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Positioning")]
    [SerializeField] private Vector2 popupOffset = new Vector2(0, 50); // Offset from target marker
    
    // Events
    public System.Action OnDriveHereClicked;
    public System.Action OnCloseClicked;
    
    private Canvas parentCanvas;
    private Camera mainCamera;
    private Transform targetTransform; // The target marker's transform
    private bool isPopupVisible = false;
    private Vector3 initialScale;
    
    void Awake()
    {
        // Cache references
        parentCanvas = GetComponentInParent<Canvas>();
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindFirstObjectByType<Camera>();
        
        // Store initial scale for animation
        if (popupPanel != null)
        {
            initialScale = popupPanel.transform.localScale;
        }
        
        // Setup button listeners
        if (driveHereButton != null)
        {
            driveHereButton.onClick.AddListener(OnDriveHereButtonClicked);
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }
        
        // Start hidden
        HidePopup(false);
    }
    
    void OnDestroy()
    {
        // Clean up button listeners
        if (driveHereButton != null)
        {
            driveHereButton.onClick.RemoveListener(OnDriveHereButtonClicked);
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        }
    }
    
    void Update()
    {
        // Update popup position if visible and target exists
        if (isPopupVisible && targetTransform != null && mainCamera != null)
        {
            UpdatePopupPosition();
        }
    }
    
    /// <summary>
    /// Shows the popup at the target position
    /// </summary>
    /// <param name="targetTransform">The transform of the target marker</param>
    /// <param name="gpsCoordinates">Optional GPS coordinates to display</param>
    public void ShowPopup(Transform targetTransform, Vector2 gpsCoordinates = default)
    {
        this.targetTransform = targetTransform;
        
        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
            isPopupVisible = true;
            
            // Update GPS text if provided
            if (locationText != null && gpsCoordinates != Vector2.zero)
            {
                locationText.text = $"GPS: {gpsCoordinates.x:F6}, {gpsCoordinates.y:F6}";
            }
            
            // Update position immediately
            UpdatePopupPosition();
            
            // Animate if enabled
            if (useAnimation)
            {
                StartCoroutine(AnimatePopupShow());
            }
            else
            {
                popupPanel.transform.localScale = initialScale;
            }
        }
    }
    
    /// <summary>
    /// Hides the popup
    /// </summary>
    /// <param name="animate">Whether to animate the hide transition</param>
    public void HidePopup(bool animate = true)
    {
        if (animate && useAnimation && isPopupVisible)
        {
            StartCoroutine(AnimatePopupHide());
        }
        else
        {
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
            isPopupVisible = false;
            targetTransform = null;
        }
    }
    
    /// <summary>
    /// Updates the popup position relative to the target marker
    /// </summary>
    void UpdatePopupPosition()
    {
        if (targetTransform == null || mainCamera == null || popupPanel == null)
            return;
        
        // Convert world position to screen position
        Vector3 screenPos = mainCamera.WorldToScreenPoint(targetTransform.position);
        
        // Check if target is visible
        if (screenPos.z < 0)
        {
            // Target is behind camera, hide popup
            HidePopup(false);
            return;
        }
        
        // Convert to canvas position
        Vector2 canvasPos;
        if (parentCanvas != null)
        {
            if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvasPos = screenPos;
            }
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentCanvas.GetComponent<RectTransform>(),
                    screenPos,
                    parentCanvas.worldCamera,
                    out canvasPos);
            }
        }
        else
        {
            canvasPos = screenPos;
        }
        
        // Apply offset
        canvasPos += popupOffset;
        
        // Set popup position
        RectTransform popupRect = popupPanel.GetComponent<RectTransform>();
        if (popupRect != null)
        {
            popupRect.position = canvasPos;
        }
    }
    
    /// <summary>
    /// Animation coroutine for showing the popup
    /// </summary>
    IEnumerator AnimatePopupShow()
    {
        if (popupPanel == null) yield break;
        
        float elapsed = 0f;
        popupPanel.transform.localScale = Vector3.zero;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / animationDuration;
            float scaleMultiplier = scaleCurve.Evaluate(progress);
            
            popupPanel.transform.localScale = initialScale * scaleMultiplier;
            
            yield return null;
        }
        
        popupPanel.transform.localScale = initialScale;
    }
    
    /// <summary>
    /// Animation coroutine for hiding the popup
    /// </summary>
    IEnumerator AnimatePopupHide()
    {
        if (popupPanel == null) yield break;
        
        float elapsed = 0f;
        Vector3 startScale = popupPanel.transform.localScale;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / animationDuration;
            float scaleMultiplier = 1f - scaleCurve.Evaluate(progress);
            
            popupPanel.transform.localScale = startScale * scaleMultiplier;
            
            yield return null;
        }
        
        popupPanel.SetActive(false);
        isPopupVisible = false;
        targetTransform = null;
    }

    /// <summary>
    /// Called when "Drive here" button is clicked
    /// </summary>
    void OnDriveHereButtonClicked()
    {
        Debug.Log("Drive here button clicked");
        OnDriveHereClicked?.Invoke();
        OnCloseClicked?.Invoke();
        HidePopup(true);
    }
    
    /// <summary>
    /// Called when "X" (close) button is clicked
    /// </summary>
    void OnCloseButtonClicked()
    {
        Debug.Log("Close button clicked");
        OnCloseClicked?.Invoke();
        HidePopup(true);
    }
    
    /// <summary>
    /// Public method to check if popup is currently visible
    /// </summary>
    public bool IsVisible()
    {
        return isPopupVisible;
    }
    
    /// <summary>
    /// Public method to set the popup offset
    /// </summary>
    public void SetPopupOffset(Vector2 offset)
    {
        popupOffset = offset;
    }
}
