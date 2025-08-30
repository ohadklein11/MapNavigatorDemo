using UnityEngine;

namespace MapNavigatorDemo.UI
{
    /// <summary>
    /// Welcome popup component that displays the welcome message using GenericPopup
    /// </summary>
    public class WelcomePopup : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GenericPopup genericPopup;
        
        [Header("Settings")]
        [SerializeField] private bool showOnStart = false;
        [SerializeField] private float delayBeforeShow = 0.5f; // Delay after loading screen hides
        
        public static WelcomePopup Instance { get; private set; }
        
        void Awake()
        {
            // Singleton pattern for easy access
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("Multiple WelcomePopup instances found. This may cause issues.");
            }
            
            // Setup popup events
            SetupPopupEvents();
        }
        
        void Start()
        {
            if (showOnStart)
            {
                ShowWelcomePopup();
            }
        }
        
        /// <summary>
        /// Sets up popup event listeners
        /// </summary>
        void SetupPopupEvents()
        {
            if (genericPopup != null)
            {
                // Listen for close button or action button clicks
                genericPopup.OnCloseButtonClicked += OnPopupClosed;
                genericPopup.OnActionButtonClicked += OnActionButtonClicked;
            }
            else
            {
                Debug.LogWarning("WelcomePopup: genericPopup reference is not assigned!");
            }
        }
        
        /// <summary>
        /// Shows the welcome popup with optional delay
        /// </summary>
        /// <param name="delay">Delay before showing the popup</param>
        public void ShowWelcomePopup(float delay = 0f)
        {
            if (delay > 0f)
            {
                StartCoroutine(ShowWithDelay(delay));
            }
            else
            {
                ShowPopupNow();
            }
        }
        
        /// <summary>
        /// Shows the welcome popup immediately
        /// </summary>
        public void ShowPopupNow()
        {
            if (genericPopup != null)
            {
                genericPopup.ShowPopup(true);
                Debug.Log("Welcome popup shown");
            }
        }
        
        /// <summary>
        /// Hides the welcome popup
        /// </summary>
        public void HideWelcomePopup()
        {
            if (genericPopup != null)
            {
                genericPopup.HidePopup(true);
            }
        }
        
        /// <summary>
        /// Shows the popup after a delay
        /// </summary>
        System.Collections.IEnumerator ShowWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            ShowPopupNow();
        }
        
        /// <summary>
        /// Called when the popup is closed
        /// </summary>
        void OnPopupClosed()
        {
            Debug.Log("Welcome popup closed by user");
            // Optional: Add any cleanup or tracking logic here
        }
        
        /// <summary>
        /// Called when an action button is clicked (if any are configured)
        /// </summary>
        void OnActionButtonClicked(int buttonIndex)
        {
            Debug.Log($"Welcome popup action button {buttonIndex} clicked");
            // Optional: Handle specific action button clicks here
        }
        
        /// <summary>
        /// Public method to be called when loading is complete
        /// </summary>
        public void OnLoadingComplete()
        {
            ShowWelcomePopup(delayBeforeShow);
        }
        
        /// <summary>
        /// Checks if the welcome popup is currently visible
        /// </summary>
        public bool IsVisible()
        {
            return genericPopup != null && genericPopup.IsVisible();
        }
        
        void OnDestroy()
        {
            // Clean up event listeners
            if (genericPopup != null)
            {
                genericPopup.OnCloseButtonClicked -= OnPopupClosed;
                genericPopup.OnActionButtonClicked -= OnActionButtonClicked;
            }
        }
    }
}
