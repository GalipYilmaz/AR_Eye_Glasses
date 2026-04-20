using UnityEngine;
using UnityEngine.EventSystems;

public class GalleryPhotoViewer : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [Header("Zoom Settings")]
    public RectTransform photoDisplay;
    public float zoomSpeed = 0.005f;
    public float minZoom = 1f;
    public float maxZoom = 5f;

    [Header("Swipe Settings")]
    public float swipeThreshold = 150f; // Distance required to dismiss

    private Vector2 initialTouchPos;

    // Reset scale and position when opened
    private void OnEnable()
    {
        if (photoDisplay != null)
        {
            photoDisplay.localScale = Vector3.one;
            photoDisplay.anchoredPosition = Vector2.zero;
        }
    }

    private void Update()
    {
        // PINCH TO ZOOM LOGIC
        if (Input.touchCount == 2)
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

            float prevMagnitude = (touchZeroPrevPos - touchOnePrevPos).magnitude;
            float currentMagnitude = (touchZero.position - touchOne.position).magnitude;

            float difference = currentMagnitude - prevMagnitude;

            Zoom(difference * zoomSpeed);
        }
    }

    private void Zoom(float increment)
    {
        if (photoDisplay == null) return;

        Vector3 targetScale = photoDisplay.localScale + new Vector3(increment, increment, increment);
        targetScale.x = Mathf.Clamp(targetScale.x, minZoom, maxZoom);
        targetScale.y = Mathf.Clamp(targetScale.y, minZoom, maxZoom);
        targetScale.z = Mathf.Clamp(targetScale.z, minZoom, maxZoom);

        photoDisplay.localScale = targetScale;
    }

    // SWIPE TO DISMISS LOGIC
    public void OnBeginDrag(PointerEventData eventData)
    {
        initialTouchPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Intentionally empty
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float distanceY = eventData.position.y - initialTouchPos.y;
        float distanceX = eventData.position.x - initialTouchPos.x;

        // Hide full screen view if swiped hard enough
        if (Mathf.Abs(distanceY) > swipeThreshold || Mathf.Abs(distanceX) > swipeThreshold)
        {
            Screen.orientation = ScreenOrientation.Portrait;
            gameObject.SetActive(false);
        }
    }
}