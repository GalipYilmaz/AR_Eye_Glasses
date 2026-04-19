using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.IO;

public class ARSceneManager : MonoBehaviour
{
    [Header("AR Objects")]
    public GameObject[] glasses;
    private int currentIndex = 0;

    [Header("UI Panels")]
    public GameObject settingsPanel;
    public GameObject glassesPanel;
    public GameObject galleryPanel;
    public Image flashScreen;

    [Header("Transform Controls")]
    public Slider scaleSlider;
    public TMP_InputField scaleInput;

    public Slider xSlider;
    public TMP_InputField xInput;

    public Slider ySlider;
    public TMP_InputField yInput;

    public Slider zSlider;
    public TMP_InputField zInput;

    private void Start()
    {
        // Initialize UI listeners to sync sliders and input fields
        scaleSlider.onValueChanged.AddListener(OnScaleSliderChanged);
        xSlider.onValueChanged.AddListener(OnXSliderChanged);
        ySlider.onValueChanged.AddListener(OnYSliderChanged);
        zSlider.onValueChanged.AddListener(OnZSliderChanged);

        scaleInput.onEndEdit.AddListener(OnScaleInputChanged);
        xInput.onEndEdit.AddListener(OnXInputChanged);
        yInput.onEndEdit.AddListener(OnYInputChanged);
        zInput.onEndEdit.AddListener(OnZInputChanged);

        UpdateGlassesVisibility();
    }

    // --- Panel Navigation Logic ---

    public void ToggleSettingsPanel()
    {
        // Switch settings and ensure other panels are closed for a clean UI
        if (settingsPanel != null) settingsPanel.SetActive(!settingsPanel.activeSelf);
        if (glassesPanel != null) glassesPanel.SetActive(false);
        if (galleryPanel != null) galleryPanel.SetActive(false);
    }

    public void ToggleGlassesPanel()
    {
        if (glassesPanel != null) glassesPanel.SetActive(!glassesPanel.activeSelf);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (galleryPanel != null) galleryPanel.SetActive(false);
    }

    public void ToggleGalleryPanel()
    {
        if (galleryPanel != null) galleryPanel.SetActive(!galleryPanel.activeSelf);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (glassesPanel != null) glassesPanel.SetActive(false);
    }

    public void CloseAllPanels()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (glassesPanel != null) glassesPanel.SetActive(false);
        if (galleryPanel != null) galleryPanel.SetActive(false);
    }

    // --- Object Selection ---

    public void SelectGlass(int index)
    {
        // Change the active model based on button index from the UI
        if (index >= 0 && index < glasses.Length)
        {
            currentIndex = index;
            UpdateGlassesVisibility();
            ToggleGlassesPanel(); // Auto-close gallery after picking
        }
    }

    private void UpdateGlassesVisibility()
    {
        for (int i = 0; i < glasses.Length; i++)
        {
            if (glasses[i] != null)
                glasses[i].SetActive(i == currentIndex);
        }
        SyncUIWithActiveObject();
    }

    // --- Transform Logic (Sliders & Inputs) ---

    private void OnScaleSliderChanged(float val) { scaleInput.text = val.ToString("F2"); ApplyTransform(); }
    private void OnXSliderChanged(float val) { xInput.text = val.ToString("F2"); ApplyTransform(); }
    private void OnYSliderChanged(float val) { yInput.text = val.ToString("F2"); ApplyTransform(); }
    private void OnZSliderChanged(float val) { zInput.text = val.ToString("F2"); ApplyTransform(); }

    private void OnScaleInputChanged(string valStr) { if (float.TryParse(valStr, out float val)) scaleSlider.value = val; }
    private void OnXInputChanged(string valStr) { if (float.TryParse(valStr, out float val)) xSlider.value = val; }
    private void OnYInputChanged(string valStr) { if (float.TryParse(valStr, out float val)) ySlider.value = val; }
    private void OnZInputChanged(string valStr) { if (float.TryParse(valStr, out float val)) zSlider.value = val; }

    private void ApplyTransform()
    {
        GameObject currentGlass = glasses[currentIndex];
        if (currentGlass == null) return;

        // Apply local position and scale to the currently selected AR object
        float s = scaleSlider.value;
        currentGlass.transform.localScale = new Vector3(s, s, s);
        currentGlass.transform.localPosition = new Vector3(xSlider.value, ySlider.value, zSlider.value);
    }

    private void SyncUIWithActiveObject()
    {
        GameObject currentGlass = glasses[currentIndex];
        if (currentGlass == null) return;

        // Fetch current object data and show it on UI elements
        scaleSlider.value = currentGlass.transform.localScale.x;
        xSlider.value = currentGlass.transform.localPosition.x;
        ySlider.value = currentGlass.transform.localPosition.y;
        zSlider.value = currentGlass.transform.localPosition.z;
    }

    // --- Photography Section ---

    public void TakeSnapshot()
    {
        StartCoroutine(CaptureRoutine());
    }

    private IEnumerator CaptureRoutine()
    {
        // Wait for the frame to finish to get a complete render
        yield return new WaitForEndOfFrame();

        int width = Screen.width;
        int height = Screen.height;

        // Create a temporary texture to render the camera view separately (hides UI)
        RenderTexture rt = new RenderTexture(width, height, 24);
        Camera.main.targetTexture = rt;
        Camera.main.Render();

        RenderTexture.active = rt;
        Texture2D screenImage = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenImage.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenImage.Apply();

        // Clean up: Reset camera and release memory
        Camera.main.targetTexture = null;
        RenderTexture.active = null;

        byte[] bytes = screenImage.EncodeToPNG();

        // Save file with a unique timestamp
        string fileName = "AR_Snap_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";
        string filePath = Application.isEditor ? fileName : Path.Combine(Application.persistentDataPath, fileName);

        File.WriteAllBytes(filePath, bytes);
        Debug.Log("Photo saved to: " + filePath);

        // Memory management: Manually destroy texture objects
        Destroy(rt);
        Destroy(screenImage);

        // Visual feedback for the user
        if (flashScreen != null) StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // Instant white burst
        flashScreen.color = new Color(1f, 1f, 1f, 1f);
        yield return new WaitForSeconds(0.05f);

        // Smooth fade out
        float alpha = 1f;
        while (alpha > 0)
        {
            alpha -= Time.deltaTime * 3f;
            flashScreen.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }
    }
}