using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MapNavigatorDemo.UI;

public class LoadingScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI statusText;
    
    [Header("Settings")]
    [SerializeField] private bool fadeOut = true;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private bool showWelcomePopupAfterLoading = true;
    
    private int totalItems = 0;
    private int completedItems = 0;
    private CanvasGroup canvasGroup;
    private bool isLoading = false;
    
    public static LoadingScreen Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }
    
    void Start()
    {
    }
    
    public void StartLoading(int totalItemsToLoad, string initialStatus = "Loading...")
    {
        totalItems = totalItemsToLoad;
        completedItems = 0;
        isLoading = true;
        
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
        
        UpdateProgress();
        SetStatusText(initialStatus);
        
        Debug.Log($"Loading started: {totalItemsToLoad} items to load");
    }
    
    public void UpdateProgress(string status = null)
    {
        if (!isLoading) return;
        
        if (progressBar != null)
        {
            float progress = totalItems > 0 ? (float)completedItems / totalItems : 0f;
            progressBar.value = progress;
        }
        
        if (progressText != null)
        {
            float percentage = totalItems > 0 ? ((float)completedItems / totalItems) * 100f : 0f;
            progressText.text = $"{completedItems}/{totalItems} ({percentage:F0}%)";
        }
        
        if (!string.IsNullOrEmpty(status))
        {
            SetStatusText(status);
        }
        
        if (completedItems >= totalItems)
        {
            CompleteLoading();
        }
    }
    
    public void IncrementProgress(string status = null)
    {
        if (!isLoading) return;
        
        completedItems++;
        UpdateProgress(status);
    }
    
    public void SetStatusText(string status)
    {
        if (statusText != null)
        {
            statusText.text = status;
        }
    }
    
    public void CompleteLoading()
    {
        if (!isLoading) return;
        
        isLoading = false;
        SetStatusText("Loading Complete!");
        
        Debug.Log("Loading completed!");
        
        if (fadeOut)
        {
            StartCoroutine(FadeOutAndHide());
        }
        else
        {
            HideLoadingScreen();
        }
    }
    
    private System.Collections.IEnumerator FadeOutAndHide()
    {
        if (canvasGroup != null)
        {
            float startAlpha = canvasGroup.alpha;
            float elapsedTime = 0f;

            while (elapsedTime < fadeOutDuration)
            {
                elapsedTime += Time.deltaTime;
                float alpha = Mathf.Lerp(startAlpha, 0f, elapsedTime / fadeOutDuration);
                canvasGroup.alpha = alpha;
                yield return null;
            }

            canvasGroup.alpha = 0f;
        }
        
        HideLoadingScreen();
    }
    
    private void HideLoadingScreen()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
        
        // Reset for next use
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
        
        // Show welcome popup after loading is complete
        if (showWelcomePopupAfterLoading)
        {
            ShowWelcomePopup();
        }
    }
    
    /// <summary>
    /// Shows the welcome popup after loading is complete
    /// </summary>
    private void ShowWelcomePopup()
    {
        // Try to find and show the welcome popup
        WelcomePopup welcomePopup = WelcomePopup.Instance;
        if (welcomePopup == null)
        {
            welcomePopup = FindFirstObjectByType<WelcomePopup>();
        }
        
        if (welcomePopup != null)
        {
            welcomePopup.OnLoadingComplete();
            Debug.Log("Welcome popup triggered after loading completion");
        }
        else
        {
            Debug.LogWarning("WelcomePopup not found in scene. Make sure a WelcomePopup component is present.");
        }
    }
}
