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
    private Vector3[] initialScales;

    [Header("UI Panels & Navigation")]
    public GameObject settingsPanel;
    public GameObject glassesPanel;
    public GameObject galleryPanel;
    public Image flashScreen;
    public RectTransform[] panelRects;

    [Header("Slide HUD System")]
    public RectTransform rightSideHUD;
    public float slideSpeed = 15f;
    private bool isHudOpen = false;
    private float closedX = 540f;
    private float openX = 0f;
    private Coroutine slideCoroutine;

    [Header("Transformation UI Components")]
    public Slider scaleSlider;
    public TMP_InputField scaleInput;
    public Slider xSlider, ySlider, zSlider;
    public TMP_InputField xInput, yInput, zInput;

    [Header("Dynamic Gallery System")]
    public GameObject thumbnailPrefab;
    public Transform galleryContent;
    public GameObject fullScreenView;
    public RawImage photoDisplay;

    private bool isSyncingUI = false;

    private void Start()
    {
        // cache the original scales of the 3D models so we can reset them later if needed
        initialScales = new Vector3[glasses.Length];
        for (int i = 0; i < glasses.Length; i++)
        {
            if (glasses[i] != null) initialScales[i] = glasses[i].transform.localScale;
        }

        // make sure the HUD starts in the closed position
        if (rightSideHUD != null)
            rightSideHUD.anchoredPosition = new Vector2(closedX, rightSideHUD.anchoredPosition.y);

        SetupListeners();
        UpdateVisibility();
        LoadGallery(); // fetch saved photos from device storage on startup
    }

    private void SetupListeners()
    {
        scaleSlider.onValueChanged.AddListener(val => { scaleInput.text = val.ToString("F2"); ApplyTransforms(); });
        xSlider.onValueChanged.AddListener(val => { xInput.text = val.ToString("F2"); ApplyTransforms(); });
        ySlider.onValueChanged.AddListener(val => { yInput.text = val.ToString("F2"); ApplyTransforms(); });
        zSlider.onValueChanged.AddListener(val => { zInput.text = val.ToString("F2"); ApplyTransforms(); });

        scaleInput.onEndEdit.AddListener(val => { if (float.TryParse(val, out float f)) scaleSlider.value = f; });
        xInput.onEndEdit.AddListener(val => { if (float.TryParse(val, out float f)) xSlider.value = f; });
        yInput.onEndEdit.AddListener(val => { if (float.TryParse(val, out float f)) ySlider.value = f; });
        zInput.onEndEdit.AddListener(val => { if (float.TryParse(val, out float f)) zSlider.value = f; });
    }

    private void Update()
    {
        // checking for screen taps
        // skip this if the full-screen photo viewer is currently active
        if (Input.GetMouseButtonDown(0) && (fullScreenView == null || !fullScreenView.activeSelf))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return; // ignored if tapping on a UI element
            HandleOutsideClick();
        }
    }

    // panel toggles
    public void ToggleRightHUD()
    {
        isHudOpen = !isHudOpen;

        // stop any ongoing slide animation before starting a new one to avoid glitchy movement
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(AnimateHUD(isHudOpen ? openX : closedX));
    }

    private IEnumerator AnimateHUD(float targetX)
    {
        // slide the panel towards the target X position
        while (Mathf.Abs(rightSideHUD.anchoredPosition.x - targetX) > 0.1f)
        {
            float nextX = Mathf.Lerp(rightSideHUD.anchoredPosition.x, targetX, Time.deltaTime * slideSpeed);
            rightSideHUD.anchoredPosition = new Vector2(nextX, rightSideHUD.anchoredPosition.y);
            yield return null;
        }
        // snap the position at the end of the animation
        rightSideHUD.anchoredPosition = new Vector2(targetX, rightSideHUD.anchoredPosition.y);
    }

    public void ToggleSettings() { bool s = !settingsPanel.activeSelf; CloseAll(); settingsPanel.SetActive(s); }
    public void ToggleGlasses() { bool s = !glassesPanel.activeSelf; CloseAll(); glassesPanel.SetActive(s); }
    public void ToggleGallery() { bool s = !galleryPanel.activeSelf; CloseAll(); galleryPanel.SetActive(s); }

    public void CloseAll()
    {
        // hide all floating panels
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (glassesPanel != null) glassesPanel.SetActive(false);
        if (galleryPanel != null) galleryPanel.SetActive(false);
    }

    // transformation and model selection
    public void SelectGlass(int index)
    {
        if (index < 0 || index >= glasses.Length) return;
        currentIndex = index;

        // reset the selected model's position and scale 
        GameObject obj = glasses[currentIndex];
        if (obj != null)
        {
            obj.transform.localPosition = new Vector3(0, 0.1f, 0);
            obj.transform.localScale = initialScales[currentIndex];
        }
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        // only show the currently selected pair of glasses
        for (int i = 0; i < glasses.Length; i++)
            if (glasses[i] != null) glasses[i].SetActive(i == currentIndex);

        SyncUI();
    }

    private void ApplyTransforms()
    {
        // bail out if the UI is currently being updated by the script itself
        if (isSyncingUI || glasses[currentIndex] == null) return;

        glasses[currentIndex].transform.localScale = initialScales[currentIndex] * scaleSlider.value;
        glasses[currentIndex].transform.localPosition = new Vector3(xSlider.value, ySlider.value, zSlider.value);
    }

    private void SyncUI()
    {
        if (glasses[currentIndex] == null) return;
        isSyncingUI = true;

        // calculate the relative scale ratio and update the sliders to match the model's current state
        float ratio = glasses[currentIndex].transform.localScale.x / initialScales[currentIndex].x;
        scaleSlider.value = ratio;
        xSlider.value = glasses[currentIndex].transform.localPosition.x;
        ySlider.value = glasses[currentIndex].transform.localPosition.y;
        zSlider.value = glasses[currentIndex].transform.localPosition.z;

        isSyncingUI = false;
    }

    public void TakeSnapshot()
    {
        StartCoroutine(CaptureScreen());
    }

    private IEnumerator CaptureScreen()
    {
        // hide the UI so it doesn't ruin the screenshot
        if (rightSideHUD != null) rightSideHUD.gameObject.SetActive(false);

        yield return new WaitForEndOfFrame();

        int width = Screen.width;
        int height = Screen.height;
        RenderTexture rt = new RenderTexture(width, height, 24);

        Camera.main.targetTexture = rt;
        Camera.main.Render();
        RenderTexture.active = rt;

        Texture2D screenShot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        screenShot.Apply();

        // cleanup texture memory
        Camera.main.targetTexture = null;
        RenderTexture.active = null;

        // encode and save the image locally
        byte[] bytes = screenShot.EncodeToPNG();
        string filename = "AR_Snap_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(path, bytes);

        Destroy(rt);

        CreateThumbnail(path); // update the gallery with the new photo
        Destroy(screenShot);

        // bring the UI back and trigger the camera flash effect
        if (rightSideHUD != null) rightSideHUD.gameObject.SetActive(true);
        if (flashScreen != null) StartCoroutine(DoFlash());
    }

    private IEnumerator DoFlash()
    {
        // creates white flash effect
        flashScreen.color = Color.white;
        yield return new WaitForSeconds(0.05f);
        float a = 1f;
        while (a > 0)
        {
            a -= Time.deltaTime * 4f; // fade out
            flashScreen.color = new Color(1, 1, 1, a);
            yield return null;
        }
    }

    // gallery system
    private void LoadGallery()
    {
        if (!Directory.Exists(Application.persistentDataPath)) return;

        // grab all saved AR snapshots and generate thumbnails for them
        string[] files = Directory.GetFiles(Application.persistentDataPath, "AR_Snap_*.png");
        foreach (string file in files)
        {
            CreateThumbnail(file);
        }
    }

    private void CreateThumbnail(string filePath)
    {
        if (thumbnailPrefab == null || galleryContent == null) return;

        // load the image bytes and convert them into a readable Texture2D
        byte[] bytes = File.ReadAllBytes(filePath);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);

        // spawn a new thumbnail in the gallery grid
        GameObject thumbObj = Instantiate(thumbnailPrefab, galleryContent);
        RawImage thumbImg = thumbObj.GetComponent<RawImage>();
        if (thumbImg != null) thumbImg.texture = tex;

        Button btn = thumbObj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => OpenFullScreen(tex));
        }
    }

    private void OpenFullScreen(Texture2D photoTex)
    {
        if (fullScreenView == null || photoDisplay == null) return;

        photoDisplay.texture = photoTex;
        fullScreenView.SetActive(true);

        // allow the user to rotate their phone to view the photo in landscape mode
        Screen.orientation = ScreenOrientation.AutoRotation;
        Screen.autorotateToPortrait = true;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
    }

    private void HandleOutsideClick()
    {
        // loop through all managed panels.
        foreach (RectTransform p in panelRects)
            if (p != null && p.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(p, Input.mousePosition)) return;

        CloseAll();
    }
}