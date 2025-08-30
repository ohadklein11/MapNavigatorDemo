using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Generic reusable popup component
/// Can be configured with different buttons and behaviors
/// </summary>
public class GenericPopup : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private Button[] actionButtons;
    [SerializeField] private Button closeButton;
    [SerializeField] private Slider randomizationSlider;

    [Header("Animation Settings")]
    [SerializeField] private bool useAnimation = true;
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Auto-Hide Settings")]
    [SerializeField] private bool autoHideOnAction = true;
    [SerializeField] private bool enableOnStart = false;

    [Header("Positioning")]
    [SerializeField] private Vector2 popupOffset = new Vector2(0, 50);
    [SerializeField] private bool followWorldPosition = false;

    // Events
    public System.Action<int> OnActionButtonClicked;
    public System.Action OnCloseButtonClicked;
    public System.Action OnPopupShown;
    public System.Action OnPopupHidden;
    
    private bool isPopupVisible = false;
    private Vector3 initialScale;
    
    private Canvas parentCanvas;
    private Camera mainCamera;
    private Transform targetTransform;
    private bool isFollowingTarget = false;
    
    void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindFirstObjectByType<Camera>();
        
        if (popupPanel != null)
        {
            initialScale = popupPanel.transform.localScale;
        }
        
        SetupButtonListeners();
        SetupSliderEvents();
        
        if (enableOnStart)
        {
            ShowPopup();
        }
        else
        {
            HidePopup(false);
        }
    }
    
    void OnDestroy()
    {
        CleanupButtonListeners();
        
        if (randomizationSlider != null)
        {
            randomizationSlider.onValueChanged.RemoveAllListeners();
        }
    }
    
    void Update()
    {
        if (isFollowingTarget && targetTransform != null && isPopupVisible && followWorldPosition)
        {
            UpdatePopupPosition();
        }
    }
    
    /// <summary>
    /// Setup all button listeners
    /// </summary>
    void SetupButtonListeners()
    {
        if (actionButtons != null)
        {
            for (int i = 0; i < actionButtons.Length; i++)
            {
                if (actionButtons[i] != null)
                {
                    int buttonIndex = i;
                    actionButtons[i].onClick.AddListener(() => OnActionButtonPressed(buttonIndex));
                }
            }
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseButtonPressed);
        }
    }
    
    /// <summary>
    /// Clean up all button listeners
    /// </summary>
    void CleanupButtonListeners()
    {
        if (actionButtons != null)
        {
            for (int i = 0; i < actionButtons.Length; i++)
            {
                if (actionButtons[i] != null)
                {
                    actionButtons[i].onClick.RemoveAllListeners();
                }
            }
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
        }
    }
    
    /// <summary>
    /// Shows the popup
    /// </summary>
    /// <param name="animate">Whether to animate the show transition</param>
    public void ShowPopup(bool animate = true)
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
            isPopupVisible = true;
            
            if (animate && useAnimation)
            {
                StartCoroutine(AnimatePopupShow());
            }
            else
            {
                popupPanel.transform.localScale = initialScale;
            }
            
            OnPopupShown?.Invoke();
        }
    }
    
    /// <summary>
    /// Hides the popup
    /// </summary>
    /// <param name="animate">Whether to animate the hide transition</param>
    public void HidePopup(bool animate = true)
    {
        StopFollowingWorldPosition();
        
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
            
            OnPopupHidden?.Invoke();
        }
    }
    
    /// <summary>
    /// Toggles the popup visibility
    /// </summary>
    public void TogglePopup()
    {
        if (isPopupVisible)
        {
            HidePopup();
        }
        else
        {
            ShowPopup();
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
        
        // Invoke event
        OnPopupHidden?.Invoke();
    }
    
    /// <summary>
    /// Called when an action button is pressed
    /// </summary>
    void OnActionButtonPressed(int buttonIndex)
    {
        Debug.Log($"Action button {buttonIndex} clicked");
        
        OnActionButtonClicked?.Invoke(buttonIndex);
        
        if (autoHideOnAction)
        {
            HidePopup(true);
        }
    }
    
    /// <summary>
    /// Called when close button is pressed
    /// </summary>
    void OnCloseButtonPressed()
    {
        Debug.Log("Close button clicked");
        
        OnCloseButtonClicked?.Invoke();
        
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
    /// Sets whether the popup should auto-hide when action buttons are clicked
    /// </summary>
    public void SetAutoHideOnAction(bool autoHide)
    {
        autoHideOnAction = autoHide;
    }
    
    /// <summary>
    /// Sets the popup panel reference (useful for runtime setup)
    /// </summary>
    public void SetPopupPanel(GameObject panel)
    {
        popupPanel = panel;
        if (panel != null)
        {
            initialScale = panel.transform.localScale;
        }
    }
    
    /// <summary>
    /// Stops following the world position
    /// </summary>
    public void StopFollowingWorldPosition()
    {
        isFollowingTarget = false;
        followWorldPosition = false;
        targetTransform = null;
    }
    
    /// <summary>
    /// Gets the randomization slider value (0-1)
    /// </summary>
    public float GetRandomizationValue()
    {
        if (randomizationSlider != null)
        {
            return randomizationSlider.value;
        }
        return 0f;
    }
    
    /// <summary>
    /// Shows the popup at a specific world position
    /// </summary>
    /// <param name="worldTarget">Transform to follow</param>
    /// <param name="animate">Whether to animate the show transition</param>
    public void ShowPopupAtWorldPosition(Transform worldTarget, bool animate = true)
    {
        targetTransform = worldTarget;
        isFollowingTarget = true;
        followWorldPosition = true;
        
        ShowPopup(animate);
        
        if (targetTransform != null)
        {
            UpdatePopupPosition();
        }
    }
    
    /// <summary>
    /// Updates the popup position relative to the target transform
    /// </summary>
    void UpdatePopupPosition()
    {
        if (targetTransform == null || mainCamera == null || popupPanel == null)
            return;
        
        Vector3 screenPos = mainCamera.WorldToScreenPoint(targetTransform.position);
        
        if (screenPos.z < 0)
        {
            HidePopup(false);
            return;
        }
        
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
        
        canvasPos += popupOffset;
        
        RectTransform popupRect = popupPanel.GetComponent<RectTransform>();
        if (popupRect != null)
        {
            popupRect.position = canvasPos;
        }
    }
    
    /// <summary>
    /// Sets up slider event listeners to prevent map dragging during slider interaction
    /// </summary>
    void SetupSliderEvents()
    {
        if (randomizationSlider != null)
        {
            randomizationSlider.onValueChanged.AddListener(OnSliderValueChanged);
            
            var eventTrigger = randomizationSlider.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTrigger == null)
            {
                eventTrigger = randomizationSlider.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            }
            
            var pointerDownEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerDownEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
            pointerDownEntry.callback.AddListener((data) => { OnSliderPointerDown(); });
            eventTrigger.triggers.Add(pointerDownEntry);
            
            var pointerUpEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerUpEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
            pointerUpEntry.callback.AddListener((data) => { OnSliderPointerUp(); });
            eventTrigger.triggers.Add(pointerUpEntry);
        }
    }
    
    /// <summary>
    /// Called when slider value changes
    /// </summary>
    void OnSliderValueChanged(float value)
    {
    }
    
    /// <summary>
    /// Called when pointer is pressed down on slider
    /// </summary>
    void OnSliderPointerDown()
    {
        CameraDragController cameraController = FindFirstObjectByType<CameraDragController>();
        if (cameraController != null)
        {
            cameraController.DisableDragging();
        }
    }
    
    /// <summary>
    /// Called when pointer is released from slider
    /// </summary>
    void OnSliderPointerUp()
    {
        StartCoroutine(ReEnableDraggingAfterDelay());
    }
    
    /// <summary>
    /// Re-enables camera dragging after a short delay
    /// </summary>
    System.Collections.IEnumerator ReEnableDraggingAfterDelay()
    {
        yield return new WaitForSeconds(0.1f);
        
        CameraDragController cameraController = FindFirstObjectByType<CameraDragController>();
        if (cameraController != null)
        {
            cameraController.EnableDragging();
        }
    }
}
