using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple script to handle settings button clicks.
/// Attach this to a UI Button to show the settings popup when clicked.
/// </summary>
[RequireComponent(typeof(Button))]
public class SettingsButton : MonoBehaviour
{
    [Header("Settings Reference")]
    [SerializeField] private SettingsManager settingsManager; // Reference to the SettingsManager
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    private Button settingsButton;
    
    void Awake()
    {
        // Get the button component
        settingsButton = GetComponent<Button>();
        
        // Find SettingsManager if not assigned
        if (settingsManager == null)
        {
            settingsManager = FindFirstObjectByType<SettingsManager>();
            
            if (settingsManager == null && showDebugInfo)
            {
                Debug.LogWarning("SettingsButton: SettingsManager not found! Please assign it manually or ensure SettingsManager exists in the scene.");
            }
        }
    }
    
    void Start()
    {
        // Add click listener to the button
        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OnSettingsButtonClicked);
            
            if (showDebugInfo)
            {
                Debug.Log("SettingsButton: Button listener added successfully");
            }
        }
        else if (showDebugInfo)
        {
            Debug.LogError("SettingsButton: Button component not found!");
        }
    }
    
    void OnDestroy()
    {
        // Clean up button listener
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
        }
    }
    
    /// <summary>
    /// Called when the settings button is clicked
    /// </summary>
    void OnSettingsButtonClicked()
    {
        if (settingsManager != null)
        {
            // Toggle the settings popup (show if hidden, hide if shown)
            settingsManager.ToggleSettings();
            
            if (showDebugInfo)
            {
                Debug.Log("SettingsButton: Settings popup toggled");
            }
        }
        else
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("SettingsButton: Cannot toggle settings - SettingsManager reference is null!");
            }
        }
    }
    
    /// <summary>
    /// Alternative method that can be called directly from UI Events (for UnityEvent setup)
    /// </summary>
    public void ShowSettings()
    {
        OnSettingsButtonClicked();
    }
    
    /// <summary>
    /// Alternative method to toggle settings instead of just showing them
    /// </summary>
    public void ToggleSettings()
    {
        if (settingsManager != null)
        {
            settingsManager.ToggleSettings();
            
            if (showDebugInfo)
            {
                Debug.Log("SettingsButton: Settings popup toggled");
            }
        }
        else if (showDebugInfo)
        {
            Debug.LogWarning("SettingsButton: Cannot toggle settings - SettingsManager reference is null!");
        }
    }
}
