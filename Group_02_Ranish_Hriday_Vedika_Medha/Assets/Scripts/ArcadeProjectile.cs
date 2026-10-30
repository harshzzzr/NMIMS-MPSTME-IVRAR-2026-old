using System.Collections;
using UnityEngine;

namespace IVRAR.Group02.SensoryStress
{
    /// <summary>
    /// Visible arcade sci-fi projectile fired from the blaster muzzle.
    /// Uses continuous swept raycasting for reliable collision detection at high speeds.
    /// Affects at most one target and triggers existing scoring, combo, and telemetry once.
    /// </summary>
    public class ArcadeProjectile : MonoBehaviour
    {
        [Header("Projectile Properties")]
        public float speed = 42f;
        public float maxLifetime = 1.8f;
        public LayerMask hitLayers = ~0;
        public Color boltColor = new Color(0.15f, 0.95f, 1.0f); // Bright arcade cyan

        private Vector3 velocity;
        private bool hasHit = false;
        private float spawnTime;
        private GameObject impactVfx;

        public void Launch(Vector3 direction, LayerMask layers)
        {
            velocity = direction.normalized * speed;
            hitLayers = layers;
            spawnTime = Time.time;
            transform.rotation = Quaternion.LookRotation(direction);

            BuildVisualRepresentation();
            Destroy(gameObject, maxLifetime);
        }

        private void BuildVisualRepresentation()
        {
            // 1. Sleek glowing projectile mesh
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Bolt_Core";
            core.transform.SetParent(transform, false);
            core.transform.localScale = new Vector3(0.12f, 0.12f, 0.35f);
            
            // Remove collider from child so raycast sweep handles hits
            Collider col = core.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Glowing emissive material
            Material boltMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            boltMat.color = boltColor;
            boltMat.EnableKeyword("_EMISSION");
            boltMat.SetColor("_EmissionColor", boltColor * 2.5f);
            core.GetComponent<MeshRenderer>().material = boltMat;

            // 2. Light source on bolt
            GameObject lightObj = new GameObject("Bolt_Light");
            lightObj.transform.SetParent(transform, false);
            Light pLight = lightObj.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = boltColor;
            pLight.range = 3.0f;
            pLight.intensity = 2.0f;

            // 3. Trail Renderer
            TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.startWidth = 0.09f;
            trail.endWidth = 0.01f;
            trail.material = boltMat;
            trail.startColor = boltColor;
            trail.endColor = new Color(boltColor.r, boltColor.g, boltColor.b, 0.0f);
        }

        private void Update()
        {
            if (hasHit) return;

            Vector3 currentPos = transform.position;
            float stepDistance = speed * Time.deltaTime;
            Vector3 stepVector = velocity * Time.deltaTime;
            Vector3 nextPos = currentPos + stepVector;

            // Continuous swept raycast check between frames
            if (Physics.Raycast(currentPos, velocity.normalized, out RaycastHit hit, stepDistance, hitLayers))
            {
                OnProjectileHit(hit);
                return;
            }

            transform.position = nextPos;
        }

        private void OnProjectileHit(RaycastHit hit)
        {
            if (hasHit) return;
            hasHit = true;
            transform.position = hit.point;

            // Look for target trial metadata on hit object or its parents
            TargetTrialMetadata target = hit.collider.GetComponentInParent<TargetTrialMetadata>();
            if (target != null)
            {
                Debug.Log($"[ArcadeProjectile] HIT Target: {target.TrialId} (Hostile={target.IsHostile}) at {hit.point}");
                target.RegisterDecision(true);
            }
            else
            {
                Debug.Log($"[ArcadeProjectile] Hit obstacle/surface: {hit.collider.name}");
            }

            SpawnImpactEffect(hit.point, hit.normal);
            Destroy(gameObject, 0.05f);
        }

        private void SpawnImpactEffect(Vector3 point, Vector3 normal)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "Impact_Flash";
            flash.transform.position = point + normal * 0.05f;
            flash.transform.localScale = Vector3.one * 0.35f;

            Collider col = flash.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Material flashMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            flashMat.color = Color.white;
            flashMat.EnableKeyword("_EMISSION");
            flashMat.SetColor("_EmissionColor", boltColor * 3.5f);
            flash.GetComponent<MeshRenderer>().material = flashMat;

            Destroy(flash, 0.08f);
        }
    }
}
