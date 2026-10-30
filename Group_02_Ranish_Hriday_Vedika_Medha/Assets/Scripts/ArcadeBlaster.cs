using System.Collections;
using UnityEngine;

namespace IVRAR.Group02.SensoryStress
{
    /// <summary>
    /// Non-realistic, stylized sci-fi blaster model and firing effects for arcade target engagement.
    /// Attached to Right Controller.
    /// </summary>
    public class ArcadeBlaster : MonoBehaviour
    {
        [Header("Blaster Visual Components")]
        public Transform muzzlePoint;
        public Light muzzleLight;
        public LineRenderer tracerLine;
        public AudioSource audioSource;
        public AudioClip fireClip;

        [Header("Effect Settings")]
        public Color blasterColor = new Color(0.1f, 0.9f, 1.0f); // Cyan sci-fi neon
        public float flashDuration = 0.06f;

        private GameObject blasterModel;

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (tracerLine == null) tracerLine = GetComponent<LineRenderer>();
            BuildBlasterModel();
        }

        public void BuildBlasterModel()
        {
            if (transform.Find("SciFi_Blaster_Model") != null)
            {
                blasterModel = transform.Find("SciFi_Blaster_Model").gameObject;
                return;
            }

            blasterModel = new GameObject("SciFi_Blaster_Model");
            blasterModel.transform.SetParent(transform, false);
            blasterModel.transform.localPosition = new Vector3(0.08f, -0.08f, 0.22f);
            blasterModel.transform.localRotation = Quaternion.identity;

            // Materials
            Material chassisMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            chassisMat.color = new Color(0.18f, 0.20f, 0.24f); // Sleek graphite

            Material neonMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            neonMat.color = blasterColor;
            neonMat.EnableKeyword("_EMISSION");
            neonMat.SetColor("_EmissionColor", blasterColor * 1.5f);

            // 1. Receiver / Body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Blaster_Receiver";
            body.transform.SetParent(blasterModel.transform, false);
            body.transform.localPosition = new Vector3(0f, 0f, 0f);
            body.transform.localScale = new Vector3(0.045f, 0.07f, 0.18f);
            Destroy(body.GetComponent<Collider>());
            body.GetComponent<MeshRenderer>().material = chassisMat;

            // 2. Energy Barrel (Cylinder pointing forward)
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Blaster_Barrel";
            barrel.transform.SetParent(blasterModel.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.015f, 0.12f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.035f, 0.08f, 0.035f);
            Destroy(barrel.GetComponent<Collider>());
            barrel.GetComponent<MeshRenderer>().material = neonMat;

            // 3. Ergonomic Angled Grip
            GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grip.name = "Blaster_Grip";
            grip.transform.SetParent(blasterModel.transform, false);
            grip.transform.localPosition = new Vector3(0f, -0.065f, -0.03f);
            grip.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            grip.transform.localScale = new Vector3(0.035f, 0.09f, 0.038f);
            Destroy(grip.GetComponent<Collider>());
            grip.GetComponent<MeshRenderer>().material = chassisMat;

            // 4. Muzzle Tip
            GameObject muzzleObj = new GameObject("Blaster_Muzzle");
            muzzleObj.transform.SetParent(blasterModel.transform, false);
            muzzleObj.transform.localPosition = new Vector3(0f, 0.015f, 0.21f);
            muzzlePoint = muzzleObj.transform;

            // 5. Muzzle Flash Light
            GameObject lightObj = new GameObject("Muzzle_Light");
            lightObj.transform.SetParent(muzzlePoint, false);
            muzzleLight = lightObj.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.color = blasterColor;
            muzzleLight.range = 2.5f;
            muzzleLight.intensity = 0f;
            muzzleLight.enabled = false;
        }

        public void FireProjectile(Vector3 origin, Vector3 direction, LayerMask targetLayers)
        {
            if (audioSource != null && fireClip != null)
                audioSource.PlayOneShot(fireClip);

            Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : origin;
            
            // Spawn visible arcade projectile
            GameObject projObj = new GameObject("Arcade_Projectile");
            projObj.transform.position = spawnPos;
            ArcadeProjectile projectile = projObj.AddComponent<ArcadeProjectile>();
            projectile.boltColor = blasterColor;
            projectile.Launch(direction, targetLayers);

            // Brief muzzle flash
            StartCoroutine(FlashMuzzleCoroutine(spawnPos + direction * 5f));
        }

        public void TriggerFireEffect(Vector3 hitPoint)
        {
            if (audioSource != null && fireClip != null)
                audioSource.PlayOneShot(fireClip);

            StartCoroutine(FlashMuzzleCoroutine(hitPoint));
        }

        private IEnumerator FlashMuzzleCoroutine(Vector3 hitPoint)
        {
            Vector3 startPos = muzzlePoint != null ? muzzlePoint.position : transform.position;

            if (muzzleLight != null)
            {
                muzzleLight.enabled = true;
                muzzleLight.intensity = 2.5f;
            }

            if (tracerLine != null)
            {
                tracerLine.startColor = blasterColor;
                tracerLine.endColor = Color.white;
                tracerLine.startWidth = 0.03f;
                tracerLine.endWidth = 0.015f;
                tracerLine.SetPosition(0, startPos);
                tracerLine.SetPosition(1, hitPoint);
            }

            yield return new WaitForSeconds(flashDuration);

            if (muzzleLight != null)
            {
                muzzleLight.intensity = 0f;
                muzzleLight.enabled = false;
            }

            if (tracerLine != null)
            {
                tracerLine.startWidth = 0.005f;
                tracerLine.endWidth = 0.002f;
                tracerLine.startColor = new Color(blasterColor.r, blasterColor.g, blasterColor.b, 0.5f);
                tracerLine.endColor = new Color(blasterColor.r, blasterColor.g, blasterColor.b, 0.2f);
            }
        }
    }
}
