using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.IO;
using UnityEngine.EventSystems;

public class ARSceneManager : MonoBehaviour
{
    [Header("AR Experience Objects")]
    public GameObject[] glasses;
    private int currentIndex = 0;
    private Vector3[] initialScales;

    [Header("UI Panels & Navigation")]
    public GameObject settingsPanel;
    public GameObject glassesPanel;
    public GameObject galleryPanel;
    public UnityEngine.UI.Image flashScreen;
    public RectTransform[] panelRects;

    [Header("Slide HUD System")]
    public RectTransform rightSideHUD;
    public float slideSpeed = 15f;
    private bool isHudOpen = false;
    private float closedX = 420f; // Adjust based on panel width
    private float openX = 0f;
    private Coroutine slideCoroutine;

    [Header("Transformation UI Components")]
    public Slider scaleSlider;
    public TMP_InputField scaleInput;
    public Slider xSlider, ySlider, zSlider;
    public TMP_InputField xInput, yInput, zInput;

    private bool isSyncingUI = false;

    private void Start()
    {
        initialScales = new Vector3[glasses.Length];
        for (int i = 0; i < glasses.Length; i++)
        {
            if (glasses[i] != null) initialScales[i] = glasses[i].transform.localScale;
        }

        // Initialize HUD in closed position
        if (rightSideHUD != null)
            rightSideHUD.anchoredPosition = new Vector2(closedX, rightSideHUD.anchoredPosition.y);

        SetupListeners();
        UpdateVisibility();
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
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;
            HandleOutsideClick();
        }
    }

    public void ToggleRightHUD()
    {
        isHudOpen = !isHudOpen;
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(AnimateHUD(isHudOpen ? openX : closedX));
    }

    private IEnumerator AnimateHUD(float targetX)
    {
        while (Mathf.Abs(rightSideHUD.anchoredPosition.x - targetX) > 0.1f)
        {
            float nextX = Mathf.Lerp(rightSideHUD.anchoredPosition.x, targetX, Time.deltaTime * slideSpeed);
            rightSideHUD.anchoredPosition = new Vector2(nextX, rightSideHUD.anchoredPosition.y);
            yield return null;
        }
        rightSideHUD.anchoredPosition = new Vector2(targetX, rightSideHUD.anchoredPosition.y);
    }

    public void ToggleSettings() { bool s = !settingsPanel.activeSelf; CloseAll(); settingsPanel.SetActive(s); }
    public void ToggleGlasses() { bool s = !glassesPanel.activeSelf; CloseAll(); glassesPanel.SetActive(s); }
    public void ToggleGallery() { bool s = !galleryPanel.activeSelf; CloseAll(); galleryPanel.SetActive(s); }

    public void CloseAll()
    {
        settingsPanel.SetActive(false);
        glassesPanel.SetActive(false);
        galleryPanel.SetActive(false);
    }

    public void SelectGlass(int index)
    {
        if (index < 0 || index >= glasses.Length) return;
        currentIndex = index;

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
        for (int i = 0; i < glasses.Length; i++)
            if (glasses[i] != null) glasses[i].SetActive(i == currentIndex);

        SyncUI();
    }

    private void ApplyTransforms()
    {
        if (isSyncingUI || glasses[currentIndex] == null) return;

        glasses[currentIndex].transform.localScale = initialScales[currentIndex] * scaleSlider.value;
        glasses[currentIndex].transform.localPosition = new Vector3(xSlider.value, ySlider.value, zSlider.value);
    }

    private void SyncUI()
    {
        if (glasses[currentIndex] == null) return;
        isSyncingUI = true;

        float ratio = glasses[currentIndex].transform.localScale.x / initialScales[currentIndex].x;
        scaleSlider.value = ratio;
        xSlider.value = glasses[currentIndex].transform.localPosition.x;
        ySlider.value = glasses[currentIndex].transform.localPosition.y;
        zSlider.value = glasses[currentIndex].transform.localPosition.z;

        isSyncingUI = false;
    }

    public void TakeSnapshot() { StartCoroutine(CaptureScreen()); }

    private IEnumerator CaptureScreen()
    {
        // Hide HUD before rendering
        if (rightSideHUD != null) rightSideHUD.gameObject.SetActive(false);

        yield return new WaitForEndOfFrame();

        RenderTexture rt = new RenderTexture(Screen.width, Screen.height, 24);
        Camera.main.targetTexture = rt;
        Camera.main.Render();
        RenderTexture.active = rt;

        Texture2D screenShot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenShot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenShot.Apply();

        Camera.main.targetTexture = null;
        RenderTexture.active = null;

        byte[] bytes = screenShot.EncodeToPNG();
        string filename = "Snap_" + System.DateTime.Now.ToString("HHmmss") + ".png";
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllBytes(path, bytes);

        Destroy(rt);
        Destroy(screenShot);

        if (rightSideHUD != null) rightSideHUD.gameObject.SetActive(true);
        if (flashScreen != null) StartCoroutine(DoFlash());
    }

    private IEnumerator DoFlash()
    {
        flashScreen.color = Color.white;
        yield return new WaitForSeconds(0.05f);
        float a = 1f;
        while (a > 0)
        {
            a -= Time.deltaTime * 4f;
            flashScreen.color = new Color(1, 1, 1, a);
            yield return null;
        }
    }

    private void HandleOutsideClick()
    {
        foreach (RectTransform p in panelRects)
            if (p.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(p, Input.mousePosition)) return;

        CloseAll();
    }
}