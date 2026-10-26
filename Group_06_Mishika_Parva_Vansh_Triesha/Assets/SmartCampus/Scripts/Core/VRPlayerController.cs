using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SmartCampus.Core
{
    [RequireComponent(typeof(CharacterController))]
    public class VRPlayerController : MonoBehaviour
    {
        [Header("Locomotion Parameters")]
        [SerializeField] private float moveSpeed = 3.0f;
        [SerializeField] private float snapTurnAngle = 45f;
        [SerializeField] private float snapTurnCooldown = 0.35f;
        [SerializeField] private float gravity = 9.81f;

        [Header("XR Rig Tracking References")]
        [SerializeField] private Transform centerEyeAnchor;
        [SerializeField] private Transform trackingSpace;

        [Header("Character Capsule Constraints")]
        [SerializeField] private float capsuleRadius = 0.3f;
        [SerializeField] private float minHeight = 1.0f;
        [SerializeField] private float maxHeight = 2.2f;

        private CharacterController characterController;
        private float verticalVelocity = 0f;
        private float lastTurnTime = 0f;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            characterController.radius = capsuleRadius;
            characterController.stepOffset = 0.25f;
            characterController.slopeLimit = 45f;
            characterController.skinWidth = 0.04f;

            LocateRigReferences();
        }

        public void LocateRigReferences()
        {
            if (centerEyeAnchor == null)
            {
                var center = transform.Find("TrackingSpace/CenterEyeAnchor");
                if (center == null)
                {
                    var found = GameObject.Find("CenterEyeAnchor");
                    if (found != null) center = found.transform;
                }
                if (center == null && Camera.main != null)
                {
                    center = Camera.main.transform;
                }
                centerEyeAnchor = center;
            }

            if (trackingSpace == null)
            {
                var ts = transform.Find("TrackingSpace");
                if (ts != null) trackingSpace = ts;
            }
        }

        private SmartCampus.UI.DestinationMenuUI cachedDestinationMenu;

        private void Update()
        {
            SynchronizeHmdCapsule();
            HandleLocomotion();
            HandleRotation();
            HandleKioskShortcut();
        }

        private void HandleKioskShortcut()
        {
            bool toggleRequested = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                toggleRequested = true;
            }
#endif

            if (!toggleRequested && (OVRInput.GetDown(OVRInput.Button.Two) || OVRInput.GetDown(OVRInput.Button.Four)))
            {
                toggleRequested = true;
            }

            if (toggleRequested)
            {
                if (cachedDestinationMenu == null)
                {
                    cachedDestinationMenu = FindAnyObjectByType<SmartCampus.UI.DestinationMenuUI>(FindObjectsInactive.Include);
                }

                if (cachedDestinationMenu != null)
                {
                    if (cachedDestinationMenu.gameObject.activeSelf)
                    {
                        cachedDestinationMenu.CloseMenu();
                    }
                    else
                    {
                        Transform hmd = (centerEyeAnchor != null) ? centerEyeAnchor : (Camera.main != null ? Camera.main.transform : transform);
                        cachedDestinationMenu.OpenMenuInFrontOfPlayer(hmd);
                    }
                }
            }
        }

        /// <summary>
        /// Dynamically aligns the CharacterController capsule with the real physical HMD offset in room-scale VR.
        /// Prevents the player from leaning or walking their head through physical walls while the capsule stays stationary.
        /// </summary>
        private void SynchronizeHmdCapsule()
        {
            if (centerEyeAnchor == null || characterController == null) return;

            // Height adjustment based on HMD eye level
            float hmdHeight = Mathf.Clamp(centerEyeAnchor.localPosition.y, minHeight, maxHeight);
            characterController.height = hmdHeight;

            // Center of capsule in local space (halfway up the HMD height, matching HMD's local X and Z offset)
            Vector3 newCenter = new Vector3(
                centerEyeAnchor.localPosition.x,
                hmdHeight * 0.5f,
                centerEyeAnchor.localPosition.z
            );

            characterController.center = newCenter;
        }

        private void HandleLocomotion()
        {
            Vector2 inputDir = Vector2.zero;

            // 1. Meta XR Primary Thumbstick (Left hand move)
            Vector2 ovrThumbstick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
            if (ovrThumbstick.sqrMagnitude > 0.05f)
            {
                inputDir = ovrThumbstick;
            }

            // 2. New Input System keyboard fallback (WASD)
#if ENABLE_INPUT_SYSTEM
            if (inputDir.sqrMagnitude < 0.05f && Keyboard.current != null)
            {
                float x = 0f;
                float y = 0f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
                inputDir = new Vector2(x, y).normalized;
            }
#endif

            // Direction oriented relative to HMD heading
            Vector3 forward = (centerEyeAnchor != null) ? centerEyeAnchor.forward : transform.forward;
            Vector3 right = (centerEyeAnchor != null) ? centerEyeAnchor.right : transform.right;

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDirection = (forward * inputDir.y + right * inputDir.x) * moveSpeed;

            // Apply gravity
            if (characterController.isGrounded)
            {
                verticalVelocity = -0.5f; // Small grounding force
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }

            moveDirection.y = verticalVelocity;

            // Move using real physics collision resolution
            characterController.Move(moveDirection * Time.deltaTime);
        }

        private void HandleRotation()
        {
            // 1. Meta XR Secondary Thumbstick (Right hand snap turn)
            Vector2 ovrTurn = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
            float turnInput = ovrTurn.x;

            // 2. New Input System Q/E snap turn fallback
#if ENABLE_INPUT_SYSTEM
            if (Mathf.Abs(turnInput) < 0.2f && Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame) turnInput = 1f;
                else if (Keyboard.current.qKey.wasPressedThisFrame) turnInput = -1f;
            }
#endif

            if (Mathf.Abs(turnInput) > 0.5f && (Time.time - lastTurnTime > snapTurnCooldown))
            {
                float turnAngle = Mathf.Sign(turnInput) * snapTurnAngle;
                SnapTurn(turnAngle);
                lastTurnTime = Time.time;
            }
        }

        public void SnapTurn(float angle)
        {
            if (centerEyeAnchor != null)
            {
                // Rotate around the player's HMD position rather than the rig root origin
                Vector3 pivot = centerEyeAnchor.position;
                transform.RotateAround(pivot, Vector3.up, angle);
            }
            else
            {
                transform.Rotate(0f, angle, 0f, Space.World);
            }
        }
    }
}
