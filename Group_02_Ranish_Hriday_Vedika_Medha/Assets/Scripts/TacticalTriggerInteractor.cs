using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IVRAR.Group02.SensoryStress
{
    /// <summary>
    /// Harmless controller raycast trigger interactor.
    /// Casts forward from the controller/prop to register engagement decisions on targets.
    /// Supports XR Controller triggers as well as Editor mouse/keyboard fallback.
    /// </summary>
    public class TacticalTriggerInteractor : MonoBehaviour
    {
        [Header("Raycast Settings")]
        public Transform rayOrigin;
        public float maxRange = 50f;
        public LayerMask targetLayerMask = ~0;
        public LineRenderer laserPointer;

        [Header("Audio Feedback")]
        public AudioSource audioSource;
        public AudioClip clickClip;

        public Group02SessionDirector sessionDirector;

        public ArcadeBlaster arcadeBlaster;

        private void Awake()
        {
            if (rayOrigin == null) rayOrigin = transform;
            if (laserPointer == null) laserPointer = GetComponent<LineRenderer>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (sessionDirector == null) sessionDirector = FindObjectOfType<Group02SessionDirector>();
            if (arcadeBlaster == null) arcadeBlaster = GetComponent<ArcadeBlaster>();
        }

        private void Update()
        {
            UpdateLaser();
            if (IsTriggerPressed())
            {
                if (sessionDirector != null && !sessionDirector.IsSessionRunning)
                {
                    sessionDirector.StartSession();
                }
                else
                {
                    PerformEngagement();
                }
            }
        }

        public Vector3 GetAimDirection()
        {
            var rightDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            if (rightDevice.isValid && rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion _))
            {
                return rayOrigin.forward;
            }
            if (Camera.main != null)
            {
                return Camera.main.transform.forward;
            }
            return rayOrigin.forward;
        }

        private void UpdateLaser()
        {
            if (laserPointer == null) return;
            Vector3 start = rayOrigin.position;
            Vector3 direction = GetAimDirection();
            Vector3 end = start + (direction * maxRange);
            if (Physics.Raycast(start, direction, out RaycastHit hit, maxRange, targetLayerMask))
            {
                end = hit.point;
            }
            laserPointer.SetPosition(0, start);
            laserPointer.SetPosition(1, end);
        }

        public bool IsTriggerPressed()
        {
            // 1. Legacy / Standard Input fallback (Mouse / Keyboard)
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                return true;

            // 2. New Input System
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) return true;
            if (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.rightTrigger.wasPressedThisFrame)) return true;
#endif

            // 3. XR Controller inputs (Meta XR Simulator / Quest HMD)
            var rightController = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            if (rightController.isValid)
            {
                if (rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool trig) && trig) return true;
                if (rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool prim) && prim) return true;
                if (rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float val) && val > 0.5f) return true;
            }

            return false;
        }

        public void PerformEngagement()
        {
            Vector3 start = rayOrigin != null ? rayOrigin.position : transform.position;
            Vector3 direction = GetAimDirection();

            if (arcadeBlaster != null)
            {
                arcadeBlaster.FireProjectile(start, direction, targetLayerMask);
            }
            else
            {
                GameObject projObj = new GameObject("Arcade_Projectile");
                projObj.transform.position = start;
                ArcadeProjectile proj = projObj.AddComponent<ArcadeProjectile>();
                proj.Launch(direction, targetLayerMask);
            }
        }
    }
}
