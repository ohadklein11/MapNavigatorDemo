using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Controller for camera dragging and zooming functionality.
/// This script allows the user to click and drag the camera view,
/// as well as zoom in and out using the mouse scroll wheel.
/// </summary>
public class CameraDragController : MonoBehaviour
{
    [Header("Camera Drag Settings")]
    [SerializeField] private bool enableDragging = true;
    [SerializeField] private float dragSpeed = 2f;
    
    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 1f;
    [SerializeField] private float minZoom = 0.25f;
    [SerializeField] private float maxZoom = 4f;
    
    private Camera cam;
    private Vector3 lastMousePosition;
    private bool isDragging = false;
    
    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }
    }
    
    void Update()
    {
        HandleMouseDrag();
        HandleZoom();
    }
    
    void HandleMouseDrag()
    {
        if (!enableDragging)
        {
            isDragging = false;
            return;
        }
        
        // Check for mouse button down - but only if not over UI
        if (Input.GetMouseButtonDown(0))
        {
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                isDragging = true;
                lastMousePosition = Input.mousePosition;
            }
        }
        
        // Check for mouse button up
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
        
        // Handle dragging - only if we started dragging and mouse is still down
        if (isDragging && Input.GetMouseButton(0))
        {
            Vector3 currentMousePosition = Input.mousePosition;
            Vector3 mouseDelta = currentMousePosition - lastMousePosition;
            
            // Convert mouse movement to world movement
            Vector3 worldDelta = cam.ScreenToWorldPoint(new Vector3(mouseDelta.x, mouseDelta.y, cam.transform.position.z));
            worldDelta = worldDelta - cam.ScreenToWorldPoint(Vector3.zero);
            
            // Move the camera
            Vector3 newPosition = cam.transform.position - worldDelta * dragSpeed;
            newPosition.z = cam.transform.position.z; // Explicitly preserve Z position
            
            cam.transform.position = newPosition;
            lastMousePosition = currentMousePosition;
        }
    }
    
    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        
        if (scroll != 0)
        {
            float newSize = cam.orthographicSize - scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
        }
    }
    
    /// <summary>
    /// Enables camera dragging functionality
    /// </summary>
    public void EnableDragging()
    {
        enableDragging = true;
    }
    
    /// <summary>
    /// Disables camera dragging functionality
    /// </summary>
    public void DisableDragging()
    {
        enableDragging = false;
        isDragging = false; // Stop any current dragging
    }
    
    /// <summary>
    /// Sets the dragging enabled state
    /// </summary>
    /// <param name="enabled">Whether dragging should be enabled</param>
    public void SetDraggingEnabled(bool enabled)
    {
        enableDragging = enabled;
        if (!enabled)
        {
            isDragging = false; // Stop any current dragging
        }
    }
}
