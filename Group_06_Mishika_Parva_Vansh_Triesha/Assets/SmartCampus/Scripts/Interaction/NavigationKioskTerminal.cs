using System;
using UnityEngine;
using SmartCampus.Core;
using SmartCampus.UI;

namespace SmartCampus.Interaction
{
    public class NavigationKioskTerminal : MonoBehaviour
    {
        [Header("Terminal Identity")]
        [SerializeField] private string terminalName = "Arrival Wayfinding Terminal";
        [SerializeField] private Transform menuSpawnPoint;
        [SerializeField] private GameObject interactionPromptVisual;

        [Header("References")]
        [SerializeField] private DestinationMenuUI destinationMenu;

        private bool isPlayerNearby = false;
        private Transform cachedPlayerHead;
        private float lastInteractTime = -999f;
        private const float INTERACT_COOLDOWN = 0.5f;

        // Terminal locations: Outdoor Plaza Kiosk (transform.position) and Lobby Reception Desk Workstation
        private static readonly Vector3 ReceptionDeskPosition = new Vector3(0.2f, 1.1f, 6.0f);
        private const float PROXIMITY_RADIUS = 4.5f;
        private const float AUTO_CLOSE_RADIUS = 6.5f;

        public string TerminalName => terminalName;
        public bool IsPlayerNearby => isPlayerNearby;

        private void Start()
        {
            if (destinationMenu == null)
            {
                destinationMenu = FindAnyObjectByType<DestinationMenuUI>(FindObjectsInactive.Include);
            }

            if (interactionPromptVisual != null)
            {
                interactionPromptVisual.SetActive(false);
            }
        }

        private Vector3 GetPlayerPosition(out Transform playerTransform)
        {
            if (cachedPlayerHead != null)
            {
                playerTransform = cachedPlayerHead;
                return cachedPlayerHead.position;
            }

            var centerEye = GameObject.Find("CenterEyeAnchor");
            if (centerEye != null)
            {
                cachedPlayerHead = centerEye.transform;
                playerTransform = cachedPlayerHead;
                return cachedPlayerHead.position;
            }

            if (Camera.main != null)
            {
                cachedPlayerHead = Camera.main.transform;
                playerTransform = cachedPlayerHead;
                return cachedPlayerHead.position;
            }

            var rig = FindAnyObjectByType<VRPlayerController>();
            if (rig != null)
            {
                playerTransform = rig.transform;
                return rig.transform.position;
            }

            var cc = FindAnyObjectByType<CharacterController>();
            if (cc != null)
            {
                playerTransform = cc.transform;
                return cc.transform.position;
            }

            playerTransform = null;
            return Vector3.zero;
        }

        private void Update()
        {
            Vector3 playerPos = GetPlayerPosition(out var playerHead);

            // Proximity check: Physics trigger OR distance to Outdoor Kiosk (< 4.5m) OR distance to Reception Desk (< 4.5m)
            float distKiosk = Vector3.Distance(transform.position, playerPos);
            float distReception = Vector3.Distance(ReceptionDeskPosition, playerPos);
            bool playerInProximity = isPlayerNearby || (playerHead != null && (distKiosk < PROXIMITY_RADIUS || distReception < PROXIMITY_RADIUS));

            if (playerInProximity && interactionPromptVisual != null && !interactionPromptVisual.activeSelf)
            {
                interactionPromptVisual.SetActive(true);
            }
            else if (!playerInProximity && interactionPromptVisual != null && interactionPromptVisual.activeSelf)
            {
                interactionPromptVisual.SetActive(false);
            }

            // Input Detection: Meta XR Primary/Secondary Index Trigger, A/X buttons, or Keyboard E / Space
            bool triggerPressed = false;

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) ||
                OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger) ||
                OVRInput.GetDown(OVRInput.Button.One) ||
                OVRInput.GetDown(OVRInput.Button.Three))
            {
                triggerPressed = true;
            }

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                (UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame ||
                 UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame))
            {
                triggerPressed = true;
            }
#endif

            // When trigger is pressed while near a terminal:
            // If the destination menu is NOT active, open it in front of the player!
            // If the destination menu IS ALREADY active, do not toggle it closed (trigger is used for UI clicking).
            if (triggerPressed && playerInProximity)
            {
                if (destinationMenu == null)
                {
                    destinationMenu = FindAnyObjectByType<DestinationMenuUI>(FindObjectsInactive.Include);
                }

                if (destinationMenu != null && !destinationMenu.gameObject.activeSelf)
                {
                    Debug.Log($"[KIOSK INPUT] Trigger/E pressed near terminal (Proximity=True). Opening Destination Menu...");
                    openedViaTerminal = true;
                    OpenMenu();
                }
            }

            // Auto-close menu ONLY if it was opened via physical terminal proximity and player walks away (> 6.5m)
            if (openedViaTerminal && !playerInProximity && destinationMenu != null && destinationMenu.gameObject.activeSelf)
            {
                if (distKiosk > AUTO_CLOSE_RADIUS && distReception > AUTO_CLOSE_RADIUS)
                {
                    Debug.Log("[KIOSK] Player walked away from terminal -> Auto-closing Destination Menu.");
                    openedViaTerminal = false;
                    destinationMenu.CloseMenu();
                }
            }
        }

        private bool openedViaTerminal = false;

        public static void NotifyRemoteKioskOpened()
        {
            var terminals = FindObjectsByType<NavigationKioskTerminal>(FindObjectsInactive.Include);
            if (terminals != null)
            {
                foreach (var t in terminals)
                {
                    t.openedViaTerminal = false;
                }
            }
        }

        public void Interact()
        {
            openedViaTerminal = true;
            OpenMenu();
        }

        public void OpenMenu()
        {
            // Debounce to prevent simultaneous double-triggers from pointer + proximity events in same frame
            if (Time.time - lastInteractTime < INTERACT_COOLDOWN)
            {
                return;
            }
            lastInteractTime = Time.time;

            if (destinationMenu == null)
            {
                destinationMenu = FindAnyObjectByType<DestinationMenuUI>(FindObjectsInactive.Include);
            }

            if (destinationMenu != null)
            {
                if (!destinationMenu.gameObject.activeSelf)
                {
                    // Place menu 1.6 meters directly in front of player's head, at comfortable eye level, facing player
                    GetPlayerPosition(out var playerHead);
                    Vector3 spawnPos;
                    Quaternion spawnRot;

                    if (playerHead != null)
                    {
                        Vector3 fwd = playerHead.forward;
                        fwd.y = 0;
                        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                        fwd.Normalize();

                        spawnPos = playerHead.position + fwd * 1.6f;
                        spawnPos.y = Mathf.Clamp(playerHead.position.y - 0.05f, 1.35f, 1.65f);
                        spawnRot = Quaternion.LookRotation(fwd, Vector3.up);
                    }
                    else
                    {
                        spawnPos = (menuSpawnPoint != null) ? menuSpawnPoint.position : transform.position - transform.forward * 0.85f + Vector3.up * 1.45f;
                        spawnRot = Quaternion.Euler(0, 155f, 0);
                    }

                    Debug.Log($"[KIOSK INTERACT] Opening DestinationMenu_Canvas at Pos={spawnPos}, Rot={spawnRot.eulerAngles}");
                    destinationMenu.OpenAtLocation(spawnPos, spawnRot);
                }
            }
            else
            {
                Debug.LogError("[KIOSK INTERACT] CRITICAL: destinationMenu reference could not be found!");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[KIOSK TRIGGER ENTER] Detected collider: '{other.gameObject.name}', tag='{other.tag}'");
            if (other.CompareTag("Player") || other.GetComponentInParent<OVRCameraRig>() != null || other.GetComponent<CharacterController>() != null)
            {
                isPlayerNearby = true;
                Debug.Log("[KIOSK TRIGGER ENTER] isPlayerNearby set to TRUE!");
                if (interactionPromptVisual != null)
                {
                    interactionPromptVisual.SetActive(true);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Debug.Log($"[KIOSK TRIGGER EXIT] Exit collider: '{other.gameObject.name}', tag='{other.tag}'");
            if (other.CompareTag("Player") || other.GetComponentInParent<OVRCameraRig>() != null || other.GetComponent<CharacterController>() != null)
            {
                isPlayerNearby = false;
                Debug.Log("[KIOSK TRIGGER EXIT] isPlayerNearby set to FALSE!");
                if (interactionPromptVisual != null)
                {
                    interactionPromptVisual.SetActive(false);
                }
            }
        }
    }
}
