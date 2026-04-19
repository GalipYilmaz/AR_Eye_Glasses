using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.IO;
using UnityEngine.EventSystems;

public class ARSceneManager : MonoBehaviour
{
    [Header("AR Objects")]
    public GameObject[] glasses;
    private int currentIndex = 0;

    // Store original sizes to avoid math errors when scaling
    private Vector3[] initialScales;

    [Header("UI Panels")]
    public GameObject settingsPanel;
    public GameObject glassesPanel;
    public GameObject galleryPanel;
    public Image flashScreen;
    public RectTransform[] panelRects;

    [Header("Transform Controls")]
    public Slider scaleSlider;
    public TMP_InputField scaleInput;

    public Slider xSlider;
    public TMP_InputField xInput;

    public Slider ySlider;
    public TMP_InputField yInput;

    public Slider zSlider;
    public TMP_InputField zInput;

    private bool isSyncingUI = false;

    private void Start()
    {
        // Save the default sizes set in the inspector
        initialScales = new Vector3[glasses.Length];
        for (int i = 0; i < glasses.Length; i++)
        {
            if (glasses[i] != null)
            {
                initialScales[i] = glasses[i].transform.localScale;
            }
        }

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

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Prevent clicking through UI elements
            if (EventSystem.current.IsPointerOverGameObject()) return;
            if (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)) return;

            HandleOutsideClick();
        }
    }

    private void HandleOutsideClick()
    {
        if (!settingsPanel.activeSelf && !glassesPanel.activeSelf && !galleryPanel.activeSelf) return;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;

        foreach (RectTransform panel in panelRects)
        {
            if (panel.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(panel, Input.mousePosition))
            {
                return;
            }
        }

        CloseAllPanels();
    }

    public void ToggleSettingsPanel() { bool state = !settingsPanel.activeSelf; CloseAllPanels(); settingsPanel.SetActive(state); }
    public void ToggleGlassesPanel() { bool state = !glassesPanel.activeSelf; CloseAllPanels(); glassesPanel.SetActive(state); }
    public void ToggleGalleryPanel() { bool state = !galleryPanel.activeSelf; CloseAllPanels(); galleryPanel.SetActive(state); }

    public void CloseAllPanels()
    {
        settingsPanel.SetActive(false);
        glassesPanel.SetActive(false);
        galleryPanel.SetActive(false);
    }

    public void SelectGlass(int index)
    {
        if (index >= 0 && index < glasses.Length)
        {
            currentIndex = index;

            // Reset position and restore original scale when switching models
            GameObject current = glasses[currentIndex];
            if (current != null)
            {
                Vector3 defaultPlacementOffset = new Vector3(0, 0.1f, 0);
                current.transform.localPosition = defaultPlacementOffset;
                current.transform.localScale = initialScales[currentIndex];
            }

            UpdateGlassesVisibility();
            ToggleGlassesPanel();
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
        if (isSyncingUI) return;

        GameObject currentGlass = glasses[currentIndex];
        if (currentGlass == null) return;

        // Treat slider value as a multiplier rather than absolute scale
        float multiplier = scaleSlider.value;
        currentGlass.transform.localScale = initialScales[currentIndex] * multiplier;

        currentGlass.transform.localPosition = new Vector3(xSlider.value, ySlider.value, zSlider.value);
    }

    private void SyncUIWithActiveObject()
    {
        GameObject currentGlass = glasses[currentIndex];
        if (currentGlass == null) return;

        isSyncingUI = true;

        // Calculate the current multiplier to update the slider correctly
        float initialX = initialScales[currentIndex].x;
        float currentMultiplier = (initialX != 0) ? (currentGlass.transform.localScale.x / initialX) : 1f;

        scaleSlider.value = currentMultiplier;
        xSlider.value = currentGlass.transform.localPosition.x;
        ySlider.value = currentGlass.transform.localPosition.y;
        zSlider.value = currentGlass.transform.localPosition.z;

        scaleInput.text = scaleSlider.value.ToString("F2");
        xInput.text = xSlider.value.ToString("F2");
        yInput.text = ySlider.value.ToString("F2");
        zInput.text = zSlider.value.ToString("F2");

        isSyncingUI = false;
    }

    public void TakeSnapshot() { StartCoroutine(CaptureRoutine()); }

    private IEnumerator CaptureRoutine()
    {
        yield return new WaitForEndOfFrame();

        int width = Screen.width;
        int height = Screen.height;
        RenderTexture rt = new RenderTexture(width, height, 24);

        Camera.main.targetTexture = rt;
        Camera.main.Render();
        RenderTexture.active = rt;

        Texture2D screenImage = new Texture2D(width, height, TextureFormat.RGB24, false);
        screenImage.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenImage.Apply();

        Camera.main.targetTexture = null;
        RenderTexture.active = null;

        byte[] bytes = screenImage.EncodeToPNG();
        string fileName = "AR_Snap_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";
        string filePath = Application.isEditor ? fileName : Path.Combine(Application.persistentDataPath, fileName);

        File.WriteAllBytes(filePath, bytes);
        Debug.Log("Photo saved to: " + filePath);

        Destroy(rt);
        Destroy(screenImage);

        if (flashScreen != null) StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        flashScreen.color = new Color(1f, 1f, 1f, 1f);
        yield return new WaitForSeconds(0.05f);

        float alpha = 1f;
        while (alpha > 0)
        {
            alpha -= Time.deltaTime * 3f;
            flashScreen.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }
    }
}