using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SmartCampus.Interaction
{
    public class XRRayPointer : MonoBehaviour
    {
        [Header("Pointer Setup")]
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private float maxRayDistance = 25f;
        [SerializeField] private LayerMask raycastLayers = ~0;
        [SerializeField] private Color normalColor = new Color(0.1f, 0.7f, 1f, 0.5f);
        [SerializeField] private Color hoverColor = new Color(0.2f, 1f, 0.5f, 0.85f);
        [SerializeField] private Color clickColor = new Color(1f, 0.85f, 0.1f, 1f);

        [Header("Visual Components")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private Transform reticle;

        private EventSystem eventSystem;
        private PointerEventData pointerData;
        private GameObject currentHoveredObject;

        private void Awake()
        {
            if (rayOrigin == null) rayOrigin = transform;

            SetupLineRenderer();
            SetupReticle();
        }

        private void Start()
        {
            eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = FindAnyObjectByType<EventSystem>();
            }
        }

        private void SetupLineRenderer()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }

            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            lineRenderer.startWidth = 0.008f;
            lineRenderer.endWidth = 0.003f;
            lineRenderer.positionCount = 2;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(s);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", normalColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", normalColor);
            lineRenderer.material = mat;
        }

        private void SetupReticle()
        {
            if (reticle == null)
            {
                var reticleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                reticleObj.name = "PointerReticle";
                reticleObj.transform.SetParent(transform);
                reticleObj.transform.localScale = Vector3.one * 0.035f;

                var col = reticleObj.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var rend = reticleObj.GetComponent<Renderer>();
                if (rend != null && lineRenderer != null)
                {
                    rend.sharedMaterial = lineRenderer.sharedMaterial;
                }

                reticle = reticleObj.transform;
            }
        }

        private void Update()
        {
            if (rayOrigin == null) return;

            Vector3 originPos = rayOrigin.position;
            Vector3 forwardDir = rayOrigin.forward;

            // Check input trigger (OVRInput or New Input System)
            bool triggerDown = CheckTriggerInput();

            // Perform raycast against UI and 3D colliders
            GameObject hitTarget = null;
            Vector3 hitPoint = originPos + forwardDir * maxRayDistance;

            // 1. Raycast UI canvases first
            hitTarget = RaycastUI(originPos, forwardDir, out var uiHitPoint);
            if (hitTarget != null)
            {
                hitPoint = uiHitPoint;
            }
            else
            {
                // 2. Raycast 3D physics colliders
                if (Physics.Raycast(originPos, forwardDir, out RaycastHit hit, maxRayDistance, raycastLayers, QueryTriggerInteraction.Ignore))
                {
                    hitPoint = hit.point;
                    hitTarget = hit.collider.gameObject;
                }
            }

            // Update laser visual
            lineRenderer.SetPosition(0, originPos);
            lineRenderer.SetPosition(1, hitPoint);

            if (reticle != null)
            {
                reticle.position = hitPoint;
                reticle.gameObject.SetActive(true);
            }

            // Update hover state
            UpdateHover(hitTarget);

            // Handle Click
            if (triggerDown && hitTarget != null)
            {
                ExecuteClick(hitTarget, hitPoint);
            }
        }

        private bool CheckTriggerInput()
        {
            // 1. Meta XR Controller check via OVRInput
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) ||
                OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger) ||
                OVRInput.GetDown(OVRInput.Button.One) ||
                OVRInput.GetDown(OVRInput.Button.Three))
            {
                return true;
            }

            // 2. New Input System fallback for Simulator / Desktop play mode
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
            {
                return true;
            }
#endif

            return false;
        }

        private GameObject RaycastUI(Vector3 origin, Vector3 direction, out Vector3 hitPoint)
        {
            hitPoint = origin + direction * maxRayDistance;

            if (eventSystem == null)
            {
                eventSystem = EventSystem.current;
                if (eventSystem == null) return null;
            }

            if (pointerData == null)
            {
                pointerData = new PointerEventData(eventSystem);
            }

            // For world-space canvas raycasting with VR pointer
            var ray = new Ray(origin, direction);
            var results = new List<RaycastResult>();

            // Find all GraphicRaycasters on active canvases
            GraphicRaycaster[] raycasters = FindObjectsByType<GraphicRaycaster>();
            for (int i = 0; i < raycasters.Length; i++)
            {
                var raycaster = raycasters[i];
                if (raycaster == null || !raycaster.isActiveAndEnabled) continue;

                var canvas = raycaster.GetComponent<Canvas>();
                if (canvas == null || canvas.renderMode != RenderMode.WorldSpace) continue;

                // Check plane intersection with canvas (ensure normal opposes ray)
                Vector3 planeNormal = canvas.transform.forward;
                if (Vector3.Dot(planeNormal, direction) > 0f) planeNormal = -planeNormal;
                Plane canvasPlane = new Plane(planeNormal, canvas.transform.position);
                if (canvasPlane.Raycast(ray, out float enterDist) && enterDist <= maxRayDistance)
                {
                    Vector3 intersect = ray.GetPoint(enterDist);
                    // Project intersect to canvas local rect using world camera or center eye
                    Camera eventCam = canvas.worldCamera;
                    if (eventCam == null)
                    {
                        var eye = GameObject.Find("CenterEyeAnchor");
                        if (eye != null) eventCam = eye.GetComponent<Camera>();
                    }
                    if (eventCam == null) eventCam = Camera.main;
                    if (canvas.worldCamera == null && eventCam != null)
                    {
                        canvas.worldCamera = eventCam;
                    }

                    Vector2 screenPos = (eventCam != null)
                        ? eventCam.WorldToScreenPoint(intersect)
                        : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

                    pointerData.position = screenPos;
                    results.Clear();
                    raycaster.Raycast(pointerData, results);

                    if (results.Count > 0)
                    {
                        hitPoint = intersect;
                        return results[0].gameObject;
                    }

                    // Fallback direct UI check: check Buttons/Toggles on this Canvas
                    var buttons = canvas.GetComponentsInChildren<Button>();
                    for (int b = 0; b < buttons.Length; b++)
                    {
                        var btn = buttons[b];
                        if (btn == null || !btn.isActiveAndEnabled || !btn.interactable) continue;
                        var bRt = btn.GetComponent<RectTransform>();
                        if (bRt != null && RectTransformUtility.RectangleContainsScreenPoint(bRt, screenPos, eventCam))
                        {
                            hitPoint = intersect;
                            return btn.gameObject;
                        }
                    }

                    var toggles = canvas.GetComponentsInChildren<Toggle>();
                    for (int t = 0; t < toggles.Length; t++)
                    {
                        var tog = toggles[t];
                        if (tog == null || !tog.isActiveAndEnabled || !tog.interactable) continue;
                        var tRt = tog.GetComponent<RectTransform>();
                        if (tRt != null && RectTransformUtility.RectangleContainsScreenPoint(tRt, screenPos, eventCam))
                        {
                            hitPoint = intersect;
                            return tog.gameObject;
                        }
                    }
                }
            }

            return null;
        }

        private void UpdateHover(GameObject newTarget)
        {
            if (newTarget != currentHoveredObject)
            {
                if (currentHoveredObject != null && eventSystem != null)
                {
                    ExecuteEvents.Execute(currentHoveredObject, pointerData, ExecuteEvents.pointerExitHandler);
                }

                currentHoveredObject = newTarget;

                if (currentHoveredObject != null && eventSystem != null)
                {
                    ExecuteEvents.Execute(currentHoveredObject, pointerData, ExecuteEvents.pointerEnterHandler);
                }

                UpdateVisualColor(currentHoveredObject != null ? hoverColor : normalColor);
            }
        }

        private void ExecuteClick(GameObject target, Vector3 point)
        {
            UpdateVisualColor(clickColor);

            if (eventSystem == null) eventSystem = EventSystem.current;
            if (pointerData == null && eventSystem != null) pointerData = new PointerEventData(eventSystem);

            // 1. Traverse hierarchy for IPointerClickHandler / Button (UI click)
            bool uiHandled = false;
            GameObject clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            if (clickHandler != null)
            {
                ExecuteEvents.Execute(clickHandler, pointerData, ExecuteEvents.pointerClickHandler);
                uiHandled = true;
            }

            if (!uiHandled)
            {
                Button btn = target.GetComponentInParent<Button>();
                if (btn != null && btn.interactable)
                {
                    btn.onClick.Invoke();
                    uiHandled = true;
                }
            }

            if (!uiHandled)
            {
                Toggle toggle = target.GetComponentInParent<Toggle>();
                if (toggle != null && toggle.interactable)
                {
                    toggle.isOn = !toggle.isOn;
                    uiHandled = true;
                }
            }

            if (uiHandled)
            {
                return;
            }

            // 2. Check 3D Interactables (Door, Kiosk)
            var door = target.GetComponentInParent<InteractableDoor>();
            if (door != null)
            {
                door.ToggleDoor();
            }

            var kiosk = target.GetComponentInParent<NavigationKioskTerminal>();
            if (kiosk == null)
            {
                string tName = target.name.ToLower();
                string pName = (target.transform.parent != null) ? target.transform.parent.name.ToLower() : "";

                // Check if target is a kiosk, monitor, workstation, reception desk, or directory board
                if (tName.Contains("kiosk") || tName.Contains("monitor") || tName.Contains("workstation") ||
                    tName.Contains("reception") || tName.Contains("directory") ||
                    pName.Contains("kiosk") || pName.Contains("workstation") || pName.Contains("reception"))
                {
                    kiosk = FindAnyObjectByType<NavigationKioskTerminal>();
                    Debug.Log($"[POINTER CLICK] Target '{target.name}' recognized as campus info terminal / workstation -> opening Wayfinding Directory!");
                }
                else
                {
                    // Check if click point is near the kiosk pedestal (2.5m) or reception desk (2.5m)
                    var anyKiosk = FindAnyObjectByType<NavigationKioskTerminal>();
                    if (anyKiosk != null)
                    {
                        float distKiosk = Vector3.Distance(point, anyKiosk.transform.position);
                        float distReception = Vector3.Distance(point, new Vector3(0.2f, 1.1f, 6.0f));
                        if (distKiosk < 2.5f || distReception < 2.5f)
                        {
                            kiosk = anyKiosk;
                            Debug.Log("[POINTER CLICK] Click point was near campus terminal -> opening Wayfinding Directory!");
                        }
                    }
                }
            }

            if (kiosk != null)
            {
                kiosk.OpenMenu();
            }
        }

        private void UpdateVisualColor(Color c)
        {
            if (lineRenderer != null && lineRenderer.material != null)
            {
                if (lineRenderer.material.HasProperty("_BaseColor")) lineRenderer.material.SetColor("_BaseColor", c);
                if (lineRenderer.material.HasProperty("_Color")) lineRenderer.material.SetColor("_Color", c);
            }
        }
    }
}
