using UnityEngine;
using UnityEngine.UI;

// Should be attached to the position transform of the marker
public class PositionMarker : MonoBehaviour
{
    [SerializeField] private Image markerImage; // Reference to the UI Image component for the marker
    [SerializeField] private float baseMarkerSize = 50f; // Base size of the marker in pixels
    [SerializeField] private bool maintainConstantSize = true; // Whether to keep constant screen size
    [SerializeField] private Vector2 anchorOffset = new Vector2(0, 0); // Offset from the anchor point (useful for bottom-anchored markers)
    
    private Camera targetCamera;
    private RectTransform markerRect;
    private Canvas parentCanvas;
    private float referenceOrthographicSize = 5f;
    
    void Start()
    {
        // Cache references
        targetCamera = Camera.main;
        if (markerImage != null)
        {
            markerRect = markerImage.GetComponent<RectTransform>();
            parentCanvas = markerImage.GetComponentInParent<Canvas>();
            
            // Set pivot to bottom center (0.5, 0) so the marker's bottom center aligns with the target position
            markerRect.pivot = new Vector2(0.5f, 0f);
        }
        
        // Store the reference camera size for scaling calculations
        if (targetCamera != null && targetCamera.orthographic)
        {
            referenceOrthographicSize = targetCamera.orthographicSize;
        }
    }

    void Update()
    {
        if (markerImage != null && targetCamera != null && markerRect != null)
        {
            // Convert world position to screen position
            Vector3 screenPos = targetCamera.WorldToScreenPoint(transform.position);
            
            // Check if the position is behind the camera or outside view
            if (screenPos.z < 0)
            {
                markerImage.gameObject.SetActive(false);
                return;
            }
            else
            {
                markerImage.gameObject.SetActive(true);
            }
            
            // Convert screen position to canvas position
            Vector2 canvasPos;
            if (parentCanvas != null)
            {
                if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvasPos = screenPos;
                }
                else
                {
                    // For world space or camera space canvases
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
            
            // Apply the anchor offset
            canvasPos += anchorOffset;
            
            // Set the marker position
            markerRect.position = canvasPos;
            
            // Handle size scaling based on zoom level
            if (maintainConstantSize)
            {
                // Keep the marker at constant screen size regardless of zoom
                markerRect.sizeDelta = new Vector2(baseMarkerSize, baseMarkerSize);
            }
        }
    }
    
    // Public method to set the anchor offset (useful for bottom-anchored markers)
    public void SetAnchorOffset(Vector2 offset)
    {
        anchorOffset = offset;
    }
    
    // Public method to set the base marker size
    public void SetBaseMarkerSize(float size)
    {
        baseMarkerSize = size;
    }
    
    // Public method to toggle constant size behavior
    public void SetMaintainConstantSize(bool maintain)
    {
        maintainConstantSize = maintain;
    }
}
