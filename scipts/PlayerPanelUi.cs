using Game.Managers.CameraManager;
using Game.Managers.EntityManager.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

public class PlayerPanelUi : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Header("UI References")]
    public Toggle tracerToggle;
    public Toggle entityMarkerToggle;
    public TMP_Dropdown entityTypeDropdown;
    public Slider rotationSlider;
    public Slider zoomSlider;
    public RectTransform panelRect;
    public Text zoomLabel;

    [Header("Access Control")]
    public string panelUserID = "default_user_id";

    public static float BuildRotationAngle { get; private set; } = 0f;

    private Vector2 dragOffset;
    private Vector2 originalPosition;
    private bool isHidden = false;

    private CameraView cameraView;

    private const float MinZoom = 0f;
    private const float MaxZoom = 100f;

    private Type selectedEntityType = null;
    private HashSet<BaseEntity> selectedTypeEntities = new HashSet<BaseEntity>();
    private Dictionary<BaseEntity, GameObject> entityArrows = new Dictionary<BaseEntity, GameObject>();

    private BasePlayer localPlayer;
    private List<Type> entityTypesCache = new List<Type>();

    void Start()
    {
        StartCoroutine(LockPanelID(panelUserID));
    }

    void OnDestroy()
    {
        StartCoroutine(UnlockPanelID(panelUserID));
    }

    IEnumerator LockPanelID(string userID)
    {
        string url = "https://judy-cir-liquid-inline.trycloudflare.com/lock";
        string json = JsonUtility.ToJson(new { userID = userID });

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            InitializePanel();
        }
        else
        {
            Debug.LogWarning("Access denied: ID already in use");
            gameObject.SetActive(false);
        }
    }

    IEnumerator UnlockPanelID(string userID)
    {
        string url = "https://judy-cir-liquid-inline.trycloudflare.com/unlock";
        string json = JsonUtility.ToJson(new { userID = userID });

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();
        Debug.Log("Lock released");
    }

    void InitializePanel()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            cameraView = mainCam.GetComponent<CameraView>();
        }

        if (tracerToggle != null)
        {
            tracerToggle.onValueChanged.AddListener(OnToggleChanged);
            OnToggleChanged(tracerToggle.isOn);
        }

        if (entityMarkerToggle != null)
        {
            entityMarkerToggle.onValueChanged.AddListener(OnEntityMarkerToggleChanged);
            OnEntityMarkerToggleChanged(entityMarkerToggle.isOn);
        }

        if (entityTypeDropdown != null)
        {
            PopulateEntityTypeDropdown();
            entityTypeDropdown.onValueChanged.AddListener(OnEntityTypeDropdownChanged);
            OnEntityTypeDropdownChanged(entityTypeDropdown.value);
        }

        if (rotationSlider != null)
        {
            rotationSlider.onValueChanged.AddListener(OnSliderChanged);
            OnSliderChanged(rotationSlider.value);
        }

        if (zoomSlider != null)
        {
            zoomSlider.onValueChanged.AddListener(OnZoomSliderChanged);

            float savedValue = PlayerPrefs.GetFloat("_sliderZoom", 0f);
            zoomSlider.value = savedValue;
            OnZoomSliderChanged(savedValue);
        }

        originalPosition = panelRect.anchoredPosition;
        FindLocalPlayer();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            panelRect.anchoredPosition = isHidden ? originalPosition : new Vector2(9999, 9999);
            isHidden = !isHidden;
        }

        if (Input.GetKeyDown(KeyCode.Z) && cameraView != null)
        {
            OnZoomSliderChanged(zoomSlider.value);
        }

        if (entityMarkerToggle != null && entityMarkerToggle.isOn && selectedEntityType != null)
        {
            DiscoverSelectedTypeEntities();
            UpdateEntityArrows();
        }
        else
        {
            CleanupAllArrows();
            selectedTypeEntities.Clear();
        }
    }

    void OnToggleChanged(bool isOn)
    {
        if (PlayerMarker.Instance != null)
        {
            PlayerMarker.Instance.IsEnabled = isOn;
        }
    }

    void OnEntityMarkerToggleChanged(bool isOn)
    {
        if (!isOn)
        {
            CleanupAllArrows();
            selectedTypeEntities.Clear();
        }
    }

    void OnSliderChanged(float value)
    {
        BuildRotationAngle = value * 360f;
    }

    void OnZoomSliderChanged(float value)
    {
        if (cameraView != null)
        {
            float zoomValue = Mathf.Lerp(MinZoom, MaxZoom, value);
            cameraView.Zoom(zoomValue, 0f);

            PlayerPrefs.SetFloat("_sliderZoom", value);
            PlayerPrefs.Save();

            if (zoomLabel != null)
            {
                zoomLabel.text = $"Zoom: {zoomValue:0.0}";
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, eventData.position, eventData.pressEventCamera, out dragOffset);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect.parent as RectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            panelRect.anchoredPosition = localPoint - dragOffset;
        }
    }

    void PopulateEntityTypeDropdown()
    {
        entityTypesCache = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsSubclassOf(typeof(BaseEntity)) && t != typeof(BasePlayer) && !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToList();

        entityTypeDropdown.options.Clear();
        foreach (var type in entityTypesCache)
        {
            entityTypeDropdown.options.Add(new TMP_Dropdown.OptionData(type.Name));
        }
    }

    void OnEntityTypeDropdownChanged(int index)
    {
        selectedEntityType = (index >= 0 && index < entityTypesCache.Count) ? entityTypesCache[index] : null;
        DiscoverSelectedTypeEntities();
        UpdateEntityArrows();
    }

    void DiscoverSelectedTypeEntities()
    {
        selectedTypeEntities.Clear();
        if (selectedEntityType == null) return;

        var allEntities = FindObjectsOfType<BaseEntity>();
        foreach (var entity in allEntities)
        {
            if (entity != null && entity.GetType() == selectedEntityType)
            {
                selectedTypeEntities.Add(entity);
            }
        }
    }

    void UpdateEntityArrows()
    {
        if (localPlayer == null || !localPlayer.gameObject.activeInHierarchy)
        {
            FindLocalPlayer();
        }

        if (localPlayer == null) return;

        List<BaseEntity> toRemove = new List<BaseEntity>();
        foreach (var kvp in entityArrows)
        {
            if (!selectedTypeEntities.Contains(kvp.Key) || kvp.Key == null)
            {
                Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var e in toRemove)
        {
            entityArrows.Remove(e);
        }

        foreach (BaseEntity entity in selectedTypeEntities)
        {
            if (entity == null || localPlayer == null) continue;

            if (!entityArrows.ContainsKey(entity))
            {
                GameObject arrowObj = new GameObject("ArrowTo_" + entity.GetType().Name);
                LineRenderer line = arrowObj.AddComponent<LineRenderer>();
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.startWidth = 0.05f;
                line.endWidth = 0.05f;
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.startColor = Color.cyan;
                line.endColor = Color.cyan;

                entityArrows[entity] = arrowObj;
            }

            LineRenderer lr = entityArrows[entity].GetComponent<LineRenderer>();
            lr.SetPosition(0, localPlayer.transform.position);
            lr.SetPosition(1, entity.transform.position);
        }
    }

    void CleanupAllArrows()
    {
        foreach (var kvp in entityArrows)
        {
            Destroy(kvp.Value);
        }
        entityArrows.Clear();
    }

    void FindLocalPlayer()
    {
        var allPlayers = FindObjectsOfType<BasePlayer>();
        foreach (BasePlayer player in allPlayers)
        {
            if (player != null && player.GetIsLocalPlayer())
            {
                localPlayer = player;
                break;
            }
        }
    }

    void OnGUI()
    {
        // Only show markers if toggle is ON
        if (entityMarkerToggle != null && entityMarkerToggle.isOn && selectedEntityType != null)
        {
            Camera cam = Camera.main;
            foreach (BaseEntity entity in selectedTypeEntities)
            {
                if (entity == null) continue;

                Vector3 worldPos = entity.transform.position;
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

                if (screenPos.z < 0) screenPos *= -1;

                Vector2 labelPos = new Vector2(screenPos.x, Screen.height - screenPos.y);

                string name = entity.GetType().Name;

                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.normal.textColor = Color.cyan;
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 12;
                style.fontStyle = FontStyle.Bold;

                GUIStyle shadowStyle = new GUIStyle(style);
                shadowStyle.normal.textColor = Color.black;

                Rect labelRect = new Rect(labelPos.x - 50, labelPos.y - 10, 100, 20);

                GUI.Label(new Rect(labelRect.x + 1, labelRect.y + 1, labelRect.width, labelRect.height), $"★ {name}", shadowStyle);
                GUI.Label(labelRect, $"★ {name}", style);
            }
        }
    }
}