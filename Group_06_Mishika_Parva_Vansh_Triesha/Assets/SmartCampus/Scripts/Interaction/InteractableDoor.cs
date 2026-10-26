using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SmartCampus.Interaction
{
    public class InteractableDoor : MonoBehaviour, IPointerClickHandler
    {
        [Header("Door Configuration")]
        [SerializeField] private Transform doorHingePivot;
        [SerializeField] private Collider doorLeafCollider;
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float openSpeed = 3.5f;
        [SerializeField] private bool startsOpen = false;
        [SerializeField] private bool isLocked = false;
        [SerializeField] private string roomName = "Room";

        [Header("Sound / Audio Feedback (Optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip closeClip;
        [SerializeField] private AudioClip lockedClip;

        private bool isOpen = false;
        private Quaternion closedRotation;
        private Quaternion targetRotation;
        private Coroutine swingCoroutine;

        public bool IsOpen => isOpen;
        public bool IsLocked => isLocked;
        public string RoomName => roomName;

        private void Awake()
        {
            if (doorHingePivot == null)
            {
                doorHingePivot = transform;
            }

            if (doorLeafCollider == null)
            {
                doorLeafCollider = GetComponentInChildren<Collider>();
            }

            closedRotation = doorHingePivot.localRotation;
            isOpen = startsOpen;

            if (isOpen)
            {
                doorHingePivot.localRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
            }
        }

        public void SetLocked(bool locked)
        {
            isLocked = locked;
            if (isLocked && isOpen)
            {
                ToggleDoor(); // Close door if locked
            }
        }

        public void ToggleDoor()
        {
            if (isLocked)
            {
                Debug.Log($"[InteractableDoor] {roomName} door is locked.");
                PlaySound(lockedClip);
                return;
            }

            isOpen = !isOpen;
            Quaternion desiredRotation = isOpen
                ? closedRotation * Quaternion.Euler(0f, openAngle, 0f)
                : closedRotation;

            if (swingCoroutine != null)
            {
                StopCoroutine(swingCoroutine);
            }

            swingCoroutine = StartCoroutine(AnimateDoor(desiredRotation));
            PlaySound(isOpen ? openClip : closeClip);
        }

        private IEnumerator AnimateDoor(Quaternion target)
        {
            while (Quaternion.Angle(doorHingePivot.localRotation, target) > 0.5f)
            {
                doorHingePivot.localRotation = Quaternion.Slerp(doorHingePivot.localRotation, target, Time.deltaTime * openSpeed);
                yield return null;
            }

            doorHingePivot.localRotation = target;
            swingCoroutine = null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            ToggleDoor();
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        // Support direct trigger zone interaction (e.g. stepping close and pressing trigger)
        private void OnTriggerStay(Collider other)
        {
            // Optional trigger interaction handled by player input or pointer
        }
    }
}
