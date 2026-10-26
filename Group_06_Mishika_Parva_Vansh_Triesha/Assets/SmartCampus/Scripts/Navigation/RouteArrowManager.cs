using System.Collections.Generic;
using UnityEngine;

namespace SmartCampus.Navigation
{
    public class RouteArrowManager : MonoBehaviour
    {
        [Header("Arrow Styling")]
        [SerializeField] private float arrowSpacing = 1.25f;
        [SerializeField] private float floorOffset = 0.04f; // Prevents z-fighting
        [SerializeField] private Vector3 arrowScale = new Vector3(0.55f, 0.08f, 0.55f);
        [SerializeField] private Color primaryArrowColor = new Color(0.0f, 0.85f, 1.0f, 0.95f); // Vibrant cyan
        [SerializeField] private Color turnArrowColor = new Color(1.0f, 0.75f, 0.1f, 0.95f);    // Amber gold for turns
        [SerializeField] private Color destinationColor = new Color(0.2f, 1.0f, 0.4f, 0.95f);   // Emerald green arrival

        [Header("Route Ribbon")]
        [SerializeField] private bool showRouteRibbon = true;
        [SerializeField] private float ribbonWidth = 0.12f;

        private Material arrowMaterial;
        private Mesh arrowMesh;
        private LineRenderer routeRibbon;
        private GameObject destinationBeacon;

        private readonly List<GameObject> activeArrows = new List<GameObject>();
        private readonly Stack<GameObject> arrowPool = new Stack<GameObject>();

        private Transform arrowContainer;

        private void Awake()
        {
            EnsureContainer();
            EnsureMaterialsAndMesh();
        }

        private void EnsureContainer()
        {
            if (arrowContainer == null)
            {
                var go = new GameObject("ActiveRouteArrows");
                go.transform.SetParent(transform);
                arrowContainer = go.transform;
            }
        }

        private void EnsureMaterialsAndMesh()
        {
            if (arrowMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                arrowMaterial = new Material(shader)
                {
                    name = "M_RouteArrow_Dynamic"
                };
                if (arrowMaterial.HasProperty("_BaseColor"))
                {
                    arrowMaterial.SetColor("_BaseColor", primaryArrowColor);
                }
                if (arrowMaterial.HasProperty("_Color"))
                {
                    arrowMaterial.SetColor("_Color", primaryArrowColor);
                }
                if (arrowMaterial.HasProperty("_EmissionColor"))
                {
                    arrowMaterial.EnableKeyword("_EMISSION");
                    arrowMaterial.SetColor("_EmissionColor", primaryArrowColor * 1.5f);
                }
            }

            if (arrowMesh == null)
            {
                arrowMesh = CreateChevronMesh();
            }

            if (showRouteRibbon && routeRibbon == null)
            {
                var ribbonObj = new GameObject("RouteRibbonLine");
                ribbonObj.transform.SetParent(transform);
                routeRibbon = ribbonObj.AddComponent<LineRenderer>();
                routeRibbon.startWidth = ribbonWidth;
                routeRibbon.endWidth = ribbonWidth;
                routeRibbon.material = arrowMaterial;
                routeRibbon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                routeRibbon.receiveShadows = false;
                routeRibbon.positionCount = 0;
            }
        }

        public void DisplayRoute(List<Vector3> waypoints, string destinationName = "")
        {
            ClearRoute();

            if (waypoints == null || waypoints.Count < 2) return;

            EnsureContainer();
            EnsureMaterialsAndMesh();

            // Setup route ribbon
            if (showRouteRibbon && routeRibbon != null)
            {
                routeRibbon.positionCount = waypoints.Count;
                for (int i = 0; i < waypoints.Count; i++)
                {
                    routeRibbon.SetPosition(i, waypoints[i] + Vector3.up * (floorOffset - 0.015f));
                }
                routeRibbon.enabled = true;
            }

            int seqIndex = 0;

            // Spawn directional arrows along each waypoint segment
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector3 start = waypoints[i];
                Vector3 end = waypoints[i + 1];

                Vector3 diff = end - start;
                float segLen = diff.magnitude;
                if (segLen < 0.2f) continue;

                Vector3 dir = diff.normalized;
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

                int count = Mathf.Max(1, Mathf.FloorToInt(segLen / arrowSpacing));
                float actualStep = segLen / count;

                for (int s = 0; s < count; s++)
                {
                    float distAlong = (s + 0.5f) * actualStep;
                    Vector3 pos = start + dir * distAlong;
                    pos.y += floorOffset;

                    GameObject arrow = GetOrCreateArrow();
                    arrow.transform.position = pos;
                    arrow.transform.rotation = rot;
                    arrow.transform.localScale = arrowScale;
                    arrow.SetActive(true);

                    var pulse = arrow.GetComponent<ArrowPulsingAnimation>();
                    if (pulse != null)
                    {
                        pulse.Initialize(primaryArrowColor, seqIndex++);
                    }

                    activeArrows.Add(arrow);
                }

                // Place turn indicator at waypoint junctions (if angle changes significantly)
                if (i < waypoints.Count - 2)
                {
                    Vector3 nextDir = (waypoints[i + 2] - waypoints[i + 1]).normalized;
                    float angle = Vector3.Angle(dir, nextDir);
                    if (angle > 30f)
                    {
                        // Floor turn indicator
                        GameObject turnRing = GetOrCreateArrow();
                        turnRing.transform.position = waypoints[i + 1] + Vector3.up * (floorOffset + 0.005f);
                        turnRing.transform.rotation = Quaternion.LookRotation(nextDir, Vector3.up);
                        turnRing.transform.localScale = arrowScale * 1.3f;
                        turnRing.SetActive(true);

                        var pulse = turnRing.GetComponent<ArrowPulsingAnimation>();
                        if (pulse != null)
                        {
                            pulse.Initialize(turnArrowColor, seqIndex++);
                        }

                        activeArrows.Add(turnRing);

                        // Larger floating turn arrow at eye level (1.5m) pointing into the turn branch
                        GameObject floatingTurnArrow = GetOrCreateArrow();
                        floatingTurnArrow.transform.position = waypoints[i + 1] + Vector3.up * 1.5f;
                        floatingTurnArrow.transform.rotation = Quaternion.LookRotation(nextDir, Vector3.up);
                        floatingTurnArrow.transform.localScale = arrowScale * 1.85f;
                        floatingTurnArrow.SetActive(true);

                        var floatPulse = floatingTurnArrow.GetComponent<ArrowPulsingAnimation>();
                        if (floatPulse != null)
                        {
                            floatPulse.Initialize(turnArrowColor, seqIndex++);
                        }

                        activeArrows.Add(floatingTurnArrow);
                    }
                }
            }

            // Spawn destination beacon at final waypoint
            SpawnDestinationBeacon(waypoints[waypoints.Count - 1], destinationName);
        }

        private void SpawnDestinationBeacon(Vector3 targetPos, string destName)
        {
            if (destinationBeacon != null)
            {
                Destroy(destinationBeacon);
            }

            destinationBeacon = new GameObject("DestinationBeacon");
            destinationBeacon.transform.SetParent(arrowContainer);
            destinationBeacon.transform.position = targetPos + Vector3.up * floorOffset;

            // Ground target ring
            var ringObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringObj.name = "ArrivalRing";
            ringObj.transform.SetParent(destinationBeacon.transform, false);
            ringObj.transform.localScale = new Vector3(1.6f, 0.02f, 1.6f);
            var ringCol = ringObj.GetComponent<Collider>();
            if (ringCol != null) Destroy(ringCol);
            var ringRend = ringObj.GetComponent<Renderer>();
            if (ringRend != null && arrowMaterial != null)
            {
                ringRend.material = arrowMaterial;
                ringRend.material.SetColor("_BaseColor", destinationColor);
            }

            // Vertical beacon pillar
            var pillarObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillarObj.name = "BeaconPillar";
            pillarObj.transform.SetParent(destinationBeacon.transform, false);
            pillarObj.transform.localPosition = new Vector3(0, 1.2f, 0);
            pillarObj.transform.localScale = new Vector3(0.08f, 1.2f, 0.08f);
            var pillarCol = pillarObj.GetComponent<Collider>();
            if (pillarCol != null) Destroy(pillarCol);
            var pillarRend = pillarObj.GetComponent<Renderer>();
            if (pillarRend != null && arrowMaterial != null)
            {
                pillarRend.material = arrowMaterial;
                pillarRend.material.SetColor("_BaseColor", destinationColor);
            }

            // Floating destination diamond
            var diamond = GameObject.CreatePrimitive(PrimitiveType.Cube);
            diamond.name = "FloatingMarker";
            diamond.transform.SetParent(destinationBeacon.transform, false);
            diamond.transform.localPosition = new Vector3(0, 2.4f, 0);
            diamond.transform.localRotation = Quaternion.Euler(45, 45, 45);
            diamond.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            var diaCol = diamond.GetComponent<Collider>();
            if (diaCol != null) Destroy(diaCol);
            var diaRend = diamond.GetComponent<Renderer>();
            if (diaRend != null && arrowMaterial != null)
            {
                diaRend.material = arrowMaterial;
                diaRend.material.SetColor("_BaseColor", destinationColor);
            }
        }

        public void UpdatePlayerProgress(Vector3 playerPos, float hideBehindDistance = 1.8f)
        {
            // Deactivate arrows player has already walked past
            for (int i = 0; i < activeArrows.Count; i++)
            {
                var arrow = activeArrows[i];
                if (arrow == null || !arrow.activeSelf) continue;

                Vector3 toArrow = arrow.transform.position - playerPos;
                Vector3 arrowForward = arrow.transform.forward;

                // If arrow is behind player's position relative to arrow heading
                if (Vector3.Dot(toArrow, arrowForward) < -hideBehindDistance)
                {
                    arrow.SetActive(false);
                }
            }
        }

        public void ClearRoute()
        {
            for (int i = 0; i < activeArrows.Count; i++)
            {
                if (activeArrows[i] != null)
                {
                    activeArrows[i].SetActive(false);
                    arrowPool.Push(activeArrows[i]);
                }
            }
            activeArrows.Clear();

            if (routeRibbon != null)
            {
                routeRibbon.positionCount = 0;
                routeRibbon.enabled = false;
            }

            if (destinationBeacon != null)
            {
                Destroy(destinationBeacon);
                destinationBeacon = null;
            }
        }

        private GameObject GetOrCreateArrow()
        {
            if (arrowPool.Count > 0)
            {
                return arrowPool.Pop();
            }

            var arrowObj = new GameObject("RouteChevron");
            arrowObj.transform.SetParent(arrowContainer);

            var filter = arrowObj.AddComponent<MeshFilter>();
            filter.sharedMesh = arrowMesh;

            var renderer = arrowObj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = arrowMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            arrowObj.AddComponent<ArrowPulsingAnimation>();

            return arrowObj;
        }

        private static Mesh CreateChevronMesh()
        {
            // Creates a sleek forward-pointing 3D chevron arrow
            var mesh = new Mesh { name = "Procedural_Chevron_Mesh" };

            // 7 vertices defining a bold forward arrow shape
            // Tip at (0, 0, 0.5), wings back at (-0.4, 0, -0.4) and (+0.4, 0, -0.4), notch at (0, 0, -0.15)
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(0f, 0f, 0.5f),        // 0: Tip
                new Vector3(0.35f, 0f, -0.35f),   // 1: Right wing tip
                new Vector3(0.15f, 0f, -0.35f),   // 2: Right inner
                new Vector3(0f, 0f, -0.15f),      // 3: Center notch
                new Vector3(-0.15f, 0f, -0.35f),  // 4: Left inner
                new Vector3(-0.35f, 0f, -0.35f),  // 5: Left wing tip
                new Vector3(0f, 0f, 0.2f)         // 6: Inner stem apex
            };

            // Triangles for double-sided visibility
            int[] triangles = new int[]
            {
                // Top face
                0, 1, 6,
                6, 1, 2,
                6, 2, 3,
                0, 6, 5,
                6, 4, 5,
                6, 3, 4,

                // Bottom face (inverted)
                6, 1, 0,
                1, 6, 2,
                2, 6, 3,
                6, 0, 5,
                4, 6, 5,
                3, 6, 4
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0.5f, 1f),
                new Vector2(1f, 0f),
                new Vector2(0.65f, 0f),
                new Vector2(0.5f, 0.35f),
                new Vector2(0.35f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0.6f)
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
