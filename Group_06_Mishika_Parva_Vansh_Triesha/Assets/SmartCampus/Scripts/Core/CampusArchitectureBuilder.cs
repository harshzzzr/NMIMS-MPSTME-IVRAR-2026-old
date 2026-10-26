using System.Collections.Generic;
using UnityEngine;
using SmartCampus.Interaction;
using SmartCampus.Navigation;

namespace SmartCampus.Core
{
    public class CampusArchitectureBuilder : MonoBehaviour
    {
        [Header("Hierarchy Containers")]
        [SerializeField] private Transform architectureRoot;
        [SerializeField] private Transform furnishingsRoot;
        [SerializeField] private Transform doorsRoot;
        [SerializeField] private Transform lightingRoot;

        private void EnsureRoots()
        {
            if (architectureRoot == null)
            {
                var go = new GameObject("1_Architecture_SolidGeometry");
                go.transform.SetParent(transform);
                architectureRoot = go.transform;
            }
            if (furnishingsRoot == null)
            {
                var go = new GameObject("2_Furnishings");
                go.transform.SetParent(transform);
                furnishingsRoot = go.transform;
            }
            if (doorsRoot == null)
            {
                var go = new GameObject("3_InteractiveDoors");
                go.transform.SetParent(transform);
                doorsRoot = go.transform;
            }
            if (lightingRoot == null)
            {
                var go = new GameObject("4_CampusLighting");
                go.transform.SetParent(transform);
                lightingRoot = go.transform;
            }
        }

        public void BuildFullCampus()
        {
            EnsureRoots();

            // Clear previous geometry under roots
            ClearChildren(architectureRoot);
            ClearChildren(furnishingsRoot);
            ClearChildren(doorsRoot);
            ClearChildren(lightingRoot);

            // 1. Arrival Plaza & Grounds
            BuildArrivalPlaza();

            // 2. Main Building Floors & Ceilings
            BuildMainStructure();

            // 3. Reception & Lobby
            BuildReceptionLobby();

            // 4. Corridors & Wing Partitions
            BuildCorridorPartitions();

            // 5. Classroom 101 & 102
            BuildClassrooms();

            // 6. Computer Lab 204
            BuildComputerLab204();

            // 7. Library
            BuildLibrary();

            // 8. Canteen
            BuildCanteen();

            // 9. Stairs & Accessible Ramp Mezzanine
            BuildStairsAndRampTerrace();

            // 10. Campus Lights & Sconces
            BuildLighting();

            Debug.Log("[CampusArchitectureBuilder] Full Campus built with solid colliders, furnished rooms, and interactive doors!");
        }

        private void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        // ==========================================
        // 1. ARRIVAL PLAZA & ENTRANCE GATE
        // ==========================================
        private void BuildArrivalPlaza()
        {
            // Plaza Paved Floor (Z: -25 to 0, X: -16 to 16)
            CreateSolidBox(architectureRoot, "Floor_Plaza", new Vector3(0, -0.1f, -12.5f), new Vector3(32, 0.2f, 25), CampusMaterialPalette.PlazaStone);

            // Left & Right Lawns
            CreateSolidBox(architectureRoot, "Lawn_West", new Vector3(-12, -0.05f, -12.5f), new Vector3(8, 0.25f, 24), CampusMaterialPalette.LawnGrass);
            CreateSolidBox(architectureRoot, "Lawn_East", new Vector3(12, -0.05f, -12.5f), new Vector3(8, 0.25f, 24), CampusMaterialPalette.LawnGrass);

            // Grand Campus Arch at Z = -23
            CreateSolidBox(architectureRoot, "Arch_Pillar_L", new Vector3(-5f, 2.5f, -23f), new Vector3(1.2f, 5f, 1.2f), CampusMaterialPalette.ArchConcrete);
            CreateSolidBox(architectureRoot, "Arch_Pillar_R", new Vector3(5f, 2.5f, -23f), new Vector3(1.2f, 5f, 1.2f), CampusMaterialPalette.ArchConcrete);
            CreateSolidBox(architectureRoot, "Arch_Lintel", new Vector3(0f, 5.2f, -23f), new Vector3(11.2f, 0.8f, 1.4f), CampusMaterialPalette.ArchConcrete);

            // Signboard on Arch
            CreateSignboard(furnishingsRoot, "Arch_Sign", new Vector3(0f, 5.2f, -22.25f), new Vector3(7.5f, 0.6f, 0.1f), "SMART CAMPUS UNIVERSITY", 28);

            // Outdoor Planters & Trees
            CreatePlanter(furnishingsRoot, new Vector3(-4.5f, 0.4f, -16f));
            CreatePlanter(furnishingsRoot, new Vector3(4.5f, 0.4f, -16f));
            CreatePlanter(furnishingsRoot, new Vector3(-4.5f, 0.4f, -8f));
            CreatePlanter(furnishingsRoot, new Vector3(4.5f, 0.4f, -8f));

            // Park Benches
            CreateParkBench(furnishingsRoot, new Vector3(-7f, 0, -12f), 90f);
            CreateParkBench(furnishingsRoot, new Vector3(7f, 0, -12f), -90f);

            // Lamp posts along plaza walkway
            CreateLampPost(lightingRoot, new Vector3(-3.2f, 0, -18f));
            CreateLampPost(lightingRoot, new Vector3(3.2f, 0, -18f));
            CreateLampPost(lightingRoot, new Vector3(-3.2f, 0, -6f));
            CreateLampPost(lightingRoot, new Vector3(3.2f, 0, -6f));

            // Exterior boundary walls / fences
            CreateSolidBox(architectureRoot, "Plaza_Fence_W", new Vector3(-16f, 1.2f, -12.5f), new Vector3(0.3f, 2.4f, 25f), CampusMaterialPalette.DarkMetal);
            CreateSolidBox(architectureRoot, "Plaza_Fence_E", new Vector3(16f, 1.2f, -12.5f), new Vector3(0.3f, 2.4f, 25f), CampusMaterialPalette.DarkMetal);
            CreateSolidBox(architectureRoot, "Plaza_Fence_S_L", new Vector3(-10.5f, 1.2f, -24.8f), new Vector3(11f, 2.4f, 0.3f), CampusMaterialPalette.DarkMetal);
            CreateSolidBox(architectureRoot, "Plaza_Fence_S_R", new Vector3(10.5f, 1.2f, -24.8f), new Vector3(11f, 2.4f, 0.3f), CampusMaterialPalette.DarkMetal);
        }

        // ==========================================
        // 2. MAIN BUILDING STRUCTURE & ENTRANCE
        // ==========================================
        private void BuildMainStructure()
        {
            // Main Interior Ground Floor (Z: 0 to 56, X: -24 to 24)
            CreateSolidBox(architectureRoot, "Floor_Interior_Ground", new Vector3(0, -0.1f, 28f), new Vector3(48, 0.2f, 56), CampusMaterialPalette.FloorTile);

            // Ceiling (Height 4.0m)
            CreateSolidBox(architectureRoot, "Ceiling_Main", new Vector3(0, 4.1f, 28f), new Vector3(48, 0.2f, 56), CampusMaterialPalette.CeilingTile);

            // Exterior Front Facade (Z = 0) with main entrance opening (X: -2.0 to +2.0)
            CreateSolidBox(architectureRoot, "Facade_Front_L", new Vector3(-13f, 2.0f, 0f), new Vector3(22f, 4.0f, 0.5f), CampusMaterialPalette.ArchConcrete);
            CreateSolidBox(architectureRoot, "Facade_Front_R", new Vector3(13f, 2.0f, 0f), new Vector3(22f, 4.0f, 0.5f), CampusMaterialPalette.ArchConcrete);
            CreateSolidBox(architectureRoot, "Facade_Front_Header", new Vector3(0f, 3.4f, 0f), new Vector3(4.0f, 1.2f, 0.5f), CampusMaterialPalette.ArchConcrete);

            // Front Entrance Sign
            CreateSignboard(furnishingsRoot, "Sign_BuildingMain", new Vector3(0f, 3.4f, -0.3f), new Vector3(3.8f, 0.6f, 0.05f), "MAIN ACADEMIC COMPLEX", 22);

            // Exterior Side and Back Walls
            CreateSolidBox(architectureRoot, "Wall_Exterior_West", new Vector3(-24f, 2.0f, 28f), new Vector3(0.5f, 4.0f, 56f), CampusMaterialPalette.ArchConcrete);
            CreateSolidBox(architectureRoot, "Wall_Exterior_East", new Vector3(24f, 2.0f, 28f), new Vector3(0.5f, 4.0f, 56f), CampusMaterialPalette.ArchConcrete);
            CreateSolidBox(architectureRoot, "Wall_Exterior_North", new Vector3(0f, 2.0f, 56f), new Vector3(48.5f, 4.0f, 0.5f), CampusMaterialPalette.ArchConcrete);
        }

        // ==========================================
        // 3. RECEPTION & MAIN LOBBY
        // ==========================================
        private void BuildReceptionLobby()
        {
            // Lobby Feature Accent Wall at Z = 16 (with hallway opening X: -2.5 to +2.5)
            CreateSolidBox(architectureRoot, "Wall_Lobby_North_L", new Vector3(-6.25f, 2.0f, 16f), new Vector3(7.5f, 4.0f, 0.4f), CampusMaterialPalette.WallAccentTeal);
            CreateSolidBox(architectureRoot, "Wall_Lobby_North_R", new Vector3(6.25f, 2.0f, 16f), new Vector3(7.5f, 4.0f, 0.4f), CampusMaterialPalette.WallAccentTeal);
            CreateSolidBox(architectureRoot, "Wall_Lobby_North_Header", new Vector3(0f, 3.4f, 16f), new Vector3(5.0f, 1.2f, 0.4f), CampusMaterialPalette.WallAccentTeal);

            // Reception Counter (Oak curved desk at X = 0, Z = 6)
            CreateSolidBox(furnishingsRoot, "Reception_Desk_Main", new Vector3(0f, 0.55f, 6.0f), new Vector3(3.4f, 1.1f, 0.8f), CampusMaterialPalette.OakWood);
            CreateSolidBox(furnishingsRoot, "Reception_Desk_Return", new Vector3(1.4f, 0.55f, 6.8f), new Vector3(0.6f, 1.1f, 1.2f), CampusMaterialPalette.OakWood);

            // Staff Computer on Desk
            CreateComputerWorkstation(furnishingsRoot, new Vector3(0.2f, 1.1f, 6.0f), 180f);

            // Reception Sign Behind Desk
            CreateSignboard(furnishingsRoot, "Sign_Reception", new Vector3(0f, 2.8f, 15.75f), new Vector3(3.2f, 0.5f, 0.05f), "INFORMATION & VISITOR SERVICES", 20);

            // Waiting Area Sofas & Coffee Table (East side of lobby)
            CreateSofa(furnishingsRoot, new Vector3(5.5f, 0f, 6.0f), -90f);
            CreateSofa(furnishingsRoot, new Vector3(5.5f, 0f, 10.0f), -90f);
            CreateSolidBox(furnishingsRoot, "Coffee_Table", new Vector3(5.5f, 0.25f, 8.0f), new Vector3(1.2f, 0.45f, 2.0f), CampusMaterialPalette.OakWood);

            // Campus Directory Board
            CreateSignboard(furnishingsRoot, "Directory_Board", new Vector3(-9.7f, 2.0f, 8.0f), new Vector3(0.05f, 1.8f, 3.2f), "CAMPUS DIRECTORY\n\nWest Wing: Classrooms 101-102 | Lab 204\nEast Wing: Central Library | Canteen\nNorth: Terrace & Mezzanine", 16);
        }

        // ==========================================
        // 4. CORRIDOR PARTITIONS & DOORS
        // ==========================================
        private void BuildCorridorPartitions()
        {
            // Main spine hallway runs from Z = 16 to Z = 42 (Width 5.0m: X: -2.5 to +2.5)

            // West corridor wall separating main hallway from Academic Wing
            // Gap 1: Doorway to West Wing Junction at Z = 24 (Z: 21.5 to 26.5)
            CreateSolidBox(architectureRoot, "Wall_Spine_W_1", new Vector3(-2.5f, 2.0f, 18.75f), new Vector3(0.3f, 4.0f, 5.5f), CampusMaterialPalette.InteriorWall);
            CreateSolidBox(architectureRoot, "Wall_Spine_W_Header_1", new Vector3(-2.5f, 3.4f, 24.0f), new Vector3(0.3f, 1.2f, 5.0f), CampusMaterialPalette.InteriorWall);
            CreateSolidBox(architectureRoot, "Wall_Spine_W_2", new Vector3(-2.5f, 2.0f, 34.25f), new Vector3(0.3f, 4.0f, 15.5f), CampusMaterialPalette.InteriorWall);

            // East corridor wall separating main hallway from Library / Canteen
            // Gap 2: Doorway to East Wing Junction at Z = 24 (Z: 21.5 to 26.5)
            CreateSolidBox(architectureRoot, "Wall_Spine_E_1", new Vector3(2.5f, 2.0f, 18.75f), new Vector3(0.3f, 4.0f, 5.5f), CampusMaterialPalette.InteriorWall);
            CreateSolidBox(architectureRoot, "Wall_Spine_E_Header_1", new Vector3(2.5f, 3.4f, 24.0f), new Vector3(0.3f, 1.2f, 5.0f), CampusMaterialPalette.InteriorWall);
            CreateSolidBox(architectureRoot, "Wall_Spine_E_2", new Vector3(2.5f, 2.0f, 34.25f), new Vector3(0.3f, 4.0f, 15.5f), CampusMaterialPalette.InteriorWall);

            // Directional Hanging Signs in Central Junction (Z = 24)
            CreateSignboard(furnishingsRoot, "Sign_Junction_West", new Vector3(0f, 3.2f, 23.5f), new Vector3(2.4f, 0.45f, 0.05f), "<-- Academic Wing (Rooms 101, 102, Lab 204)", 15);
            CreateSignboard(furnishingsRoot, "Sign_Junction_East", new Vector3(0f, 3.2f, 24.5f), new Vector3(2.4f, 0.45f, 0.05f), "Central Library & Canteen -->", 15);
        }

        // ==========================================
        // 5. CLASSROOMS 101 & 102
        // ==========================================
        private void BuildClassrooms()
        {
            // Academic Wing Hallway: X from -2.5 to -22, Z around 24
            // Classroom 101 (X: -22 to -10, Z: 14 to 22)
            // East wall facing hallway at X = -10 (Z: 14 to 22) with doorway at Z = 18
            CreateWallWithDoorway(architectureRoot, "Wall_Class101_East", new Vector3(-10f, 0, 18f), 8f, 1.4f, 2.4f, true);
            CreateSolidBox(architectureRoot, "Wall_Class101_South", new Vector3(-16f, 2.0f, 14f), new Vector3(12.5f, 4.0f, 0.3f), CampusMaterialPalette.InteriorWall);
            CreateSolidBox(architectureRoot, "Wall_Class101_North", new Vector3(-16f, 2.0f, 22f), new Vector3(12.5f, 4.0f, 0.3f), CampusMaterialPalette.InteriorWall);

            // Interactive Door for Room 101
            CreateInteractiveHingedDoor(doorsRoot, "Door_Classroom101", new Vector3(-10f, 0f, 17.3f), "Classroom 101");
            CreateSignboard(furnishingsRoot, "Sign_Class101", new Vector3(-9.8f, 2.2f, 19.2f), new Vector3(0.05f, 0.3f, 0.6f), "101\nLecture Hall A", 14);

            // Furnish Classroom 101: Front whiteboard on West wall
            CreateSolidBox(furnishingsRoot, "Whiteboard_101", new Vector3(-21.8f, 2.0f, 18.0f), new Vector3(0.05f, 1.4f, 4.0f), CampusMaterialPalette.Whiteboard);
            CreateSolidBox(furnishingsRoot, "Podium_101", new Vector3(-19.5f, 0.55f, 18.0f), new Vector3(0.8f, 1.1f, 0.6f), CampusMaterialPalette.OakWood);

            // Student Desks and Chairs (3 rows of 2 pairs)
            for (int r = 0; r < 3; r++)
            {
                float x = -16.5f + r * 2.4f;
                CreateStudentDeskPair(furnishingsRoot, new Vector3(x, 0, 16.5f), 90f);
                CreateStudentDeskPair(furnishingsRoot, new Vector3(x, 0, 19.5f), 90f);
            }

            // Classroom 102 (X: -22 to -10, Z: 26 to 34)
            CreateWallWithDoorway(architectureRoot, "Wall_Class102_East", new Vector3(-10f, 0, 30f), 8f, 1.4f, 2.4f, true);
            CreateSolidBox(architectureRoot, "Wall_Class102_South", new Vector3(-16f, 2.0f, 26f), new Vector3(12.5f, 4.0f, 0.3f), CampusMaterialPalette.InteriorWall);
            CreateSolidBox(architectureRoot, "Wall_Class102_North", new Vector3(-16f, 2.0f, 34f), new Vector3(12.5f, 4.0f, 0.3f), CampusMaterialPalette.InteriorWall);

            // Interactive Door for Room 102
            CreateInteractiveHingedDoor(doorsRoot, "Door_Classroom102", new Vector3(-10f, 0f, 29.3f), "Classroom 102");
            CreateSignboard(furnishingsRoot, "Sign_Class102", new Vector3(-9.8f, 2.2f, 31.2f), new Vector3(0.05f, 0.3f, 0.6f), "102\nSeminar Room B", 14);

            // Furnish Classroom 102
            CreateSolidBox(furnishingsRoot, "Whiteboard_102", new Vector3(-21.8f, 2.0f, 30.0f), new Vector3(0.05f, 1.4f, 4.0f), CampusMaterialPalette.Whiteboard);
            for (int r = 0; r < 3; r++)
            {
                float x = -16.5f + r * 2.4f;
                CreateStudentDeskPair(furnishingsRoot, new Vector3(x, 0, 28.5f), 90f);
                CreateStudentDeskPair(furnishingsRoot, new Vector3(x, 0, 31.5f), 90f);
            }
        }

        // ==========================================
        // 6. COMPUTER LAB 204 (KEY DESTINATION)
        // ==========================================
        private void BuildComputerLab204()
        {
            // Lab 204: X from -22 to -10, Z from 36 to 52
            CreateWallWithDoorway(architectureRoot, "Wall_Lab204_East", new Vector3(-10f, 0, 44f), 16f, 1.6f, 2.4f, true);
            CreateSolidBox(architectureRoot, "Wall_Lab204_South", new Vector3(-16f, 2.0f, 36f), new Vector3(12.5f, 4.0f, 0.3f), CampusMaterialPalette.InteriorWall);
            CreateSolidBox(architectureRoot, "Wall_Lab204_North", new Vector3(-16f, 2.0f, 52f), new Vector3(12.5f, 4.0f, 0.3f), CampusMaterialPalette.InteriorWall);

            // Interactive Door for Lab 204
            CreateInteractiveHingedDoor(doorsRoot, "Door_Lab204", new Vector3(-10f, 0f, 43.2f), "Computer Lab 204");
            CreateSignboard(furnishingsRoot, "Sign_Lab204", new Vector3(-9.8f, 2.2f, 45.4f), new Vector3(0.05f, 0.45f, 1.2f), "LAB 204\nADVANCED COMPUTING", 16);

            // Front Projector Screen & Instructor Desk
            CreateSolidBox(furnishingsRoot, "Lab204_Screen", new Vector3(-21.8f, 2.2f, 44.0f), new Vector3(0.05f, 1.8f, 5.0f), CampusMaterialPalette.Whiteboard);
            CreateSolidBox(furnishingsRoot, "Lab204_InstructorDesk", new Vector3(-19.5f, 0.4f, 44.0f), new Vector3(0.8f, 0.8f, 2.0f), CampusMaterialPalette.OakWood);
            CreateComputerWorkstation(furnishingsRoot, new Vector3(-19.5f, 0.8f, 44.0f), 90f);

            // Workstation Benches with multiple PCs (Row A & Row B)
            CreateComputerBenchRow(furnishingsRoot, new Vector3(-16.0f, 0, 40.5f), 4);
            CreateComputerBenchRow(furnishingsRoot, new Vector3(-16.0f, 0, 47.5f), 4);

            // Server Racks on North Wall with blinking LED status lights
            CreateServerRack(furnishingsRoot, new Vector3(-13.0f, 0, 51.2f));
            CreateServerRack(furnishingsRoot, new Vector3(-14.2f, 0, 51.2f));
            CreateServerRack(furnishingsRoot, new Vector3(-15.4f, 0, 51.2f));
        }

        // ==========================================
        // 7. CENTRAL LIBRARY
        // ==========================================
        private void BuildLibrary()
        {
            // Library: X from 10 to 22, Z from 26 to 52
            CreateWallWithDoorway(architectureRoot, "Wall_Library_West", new Vector3(10f, 0, 39f), 26f, 1.6f, 2.4f, true);
            CreateSolidBox(architectureRoot, "Wall_Library_South", new Vector3(16f, 2.0f, 26f), new Vector3(12.5f, 4.0f, 0.3f), CampusMaterialPalette.InteriorWall);
            CreateSolidBox(architectureRoot, "Wall_Library_North", new Vector3(16f, 2.0f, 52f), new Vector3(12.5f, 4.0f, 0.3f), CampusMaterialPalette.InteriorWall);

            // Interactive Door for Library
            CreateInteractiveHingedDoor(doorsRoot, "Door_Library", new Vector3(10f, 0f, 38.2f), "Central Library");
            CreateSignboard(furnishingsRoot, "Sign_Library", new Vector3(9.8f, 2.2f, 40.4f), new Vector3(0.05f, 0.45f, 1.2f), "CENTRAL LIBRARY\nQUIET STUDY ZONE", 16);

            // Librarian Checkout Counter
            CreateSolidBox(furnishingsRoot, "Library_Counter", new Vector3(13.0f, 0.55f, 39.0f), new Vector3(1.0f, 1.1f, 3.0f), CampusMaterialPalette.OakWood);
            CreateComputerWorkstation(furnishingsRoot, new Vector3(13.0f, 1.1f, 39.0f), -90f);

            // Rows of Tall Bookcases (East side)
            for (int b = 0; b < 4; b++)
            {
                float z = 32.0f + b * 4.5f;
                CreateTallBookcase(furnishingsRoot, new Vector3(19.0f, 0, z), 0f);
            }

            // Private Study Carrels & Reading Desks (West side of library)
            for (int s = 0; s < 3; s++)
            {
                float z = 29.0f + s * 4.0f;
                CreateStudyCarrel(furnishingsRoot, new Vector3(14.0f, 0, z));
            }
        }

        // ==========================================
        // 8. STUDENT CANTEEN / CAFETERIA
        // ==========================================
        private void BuildCanteen()
        {
            // Canteen: X from 10 to 22, Z from 14 to 24
            CreateWallWithDoorway(architectureRoot, "Wall_Canteen_West", new Vector3(10f, 0, 19f), 10f, 1.6f, 2.4f, true);
            CreateSolidBox(architectureRoot, "Wall_Canteen_South", new Vector3(16f, 2.0f, 14f), new Vector3(12.5f, 4.0f, 0.3f), CampusMaterialPalette.InteriorWall);
            CreateSolidBox(architectureRoot, "Wall_Canteen_North", new Vector3(16f, 2.0f, 24f), new Vector3(12.5f, 4.0f, 0.3f), CampusMaterialPalette.InteriorWall);

            // Interactive Door for Canteen
            CreateInteractiveHingedDoor(doorsRoot, "Door_Canteen", new Vector3(10f, 0f, 18.2f), "Student Canteen");
            CreateSignboard(furnishingsRoot, "Sign_Canteen", new Vector3(9.8f, 2.2f, 20.4f), new Vector3(0.05f, 0.4f, 1.0f), "CAMPUS BISTRO\n& CANTEEN", 16);

            // Food Serving Counter & Cashier
            CreateSolidBox(furnishingsRoot, "Canteen_Counter", new Vector3(19.5f, 0.55f, 19.0f), new Vector3(1.2f, 1.1f, 6.0f), CampusMaterialPalette.DarkMetal);
            CreateSolidBox(furnishingsRoot, "Canteen_TrayRail", new Vector3(18.7f, 0.45f, 19.0f), new Vector3(0.3f, 0.9f, 5.8f), CampusMaterialPalette.DarkMetal);

            // Dining Tables with 4 Chairs each
            CreateDiningTableSet(furnishingsRoot, new Vector3(13.5f, 0, 16.5f));
            CreateDiningTableSet(furnishingsRoot, new Vector3(16.0f, 0, 16.5f));
            CreateDiningTableSet(furnishingsRoot, new Vector3(13.5f, 0, 21.5f));
            CreateDiningTableSet(furnishingsRoot, new Vector3(16.0f, 0, 21.5f));

            // Drink Vending Machine
            CreateSolidBox(furnishingsRoot, "Vending_Machine", new Vector3(11.0f, 1.0f, 23.4f), new Vector3(1.0f, 2.0f, 0.8f), CampusMaterialPalette.WallAccentTeal);
        }

        // ==========================================
        // 9. STAIRS & ACCESSIBLE RAMP (ELEVATED MEZZANINE)
        // ==========================================
        private void BuildStairsAndRampTerrace()
        {
            // North Elevated Mezzanine Terrace at Z = 48 to 55, X: -5 to +5, Height Y = 1.8m
            CreateSolidBox(architectureRoot, "Mezzanine_Floor", new Vector3(0f, 1.7f, 51.5f), new Vector3(10.5f, 0.2f, 7.0f), CampusMaterialPalette.FloorTile);

            // Guardrails around elevated edge
            CreateSolidBox(architectureRoot, "Mezzanine_Rail_N", new Vector3(0f, 2.3f, 54.8f), new Vector3(10.5f, 1.0f, 0.1f), CampusMaterialPalette.DarkMetal);
            CreateSolidBox(architectureRoot, "Mezzanine_Rail_W", new Vector3(-5.2f, 2.3f, 51.5f), new Vector3(0.1f, 1.0f, 7.0f), CampusMaterialPalette.DarkMetal);
            CreateSolidBox(architectureRoot, "Mezzanine_Rail_E", new Vector3(5.2f, 2.3f, 51.5f), new Vector3(0.1f, 1.0f, 7.0f), CampusMaterialPalette.DarkMetal);

            // 1. Right side: Flight of Stairs (X = 2.5m, Z from 42 to 48, rising from Y = 0 to 1.8m)
            int numSteps = 10;
            float stepDepth = 0.6f;
            float stepRise = 1.8f / numSteps;
            for (int s = 0; s < numSteps; s++)
            {
                float z = 42.0f + s * stepDepth + stepDepth * 0.5f;
                float y = (s + 0.5f) * stepRise;
                CreateSolidBox(architectureRoot, $"Stair_Step_{s}", new Vector3(2.5f, y * 0.5f, z), new Vector3(2.4f, y, stepDepth), CampusMaterialPalette.PlazaStone);
            }
            CreateSignboard(furnishingsRoot, "Sign_Stairs", new Vector3(2.5f, 2.2f, 41.8f), new Vector3(1.2f, 0.35f, 0.05f), "STAIRS TO TERRACE\n[Not Step-Free]", 13);

            // 2. Left side: Smooth ADA-Compliant Ramp (X = -2.5m, Z from 42 to 48, slope rising smoothly to 1.8m)
            // Procedural ramp with physical box tilted or layered steps
            var rampObj = new GameObject("Accessible_Ramp");
            rampObj.transform.SetParent(architectureRoot);
            rampObj.transform.position = new Vector3(-2.5f, 0.9f, 45.0f);
            float rampLength = 6.0f;
            float rampAngle = Mathf.Atan2(1.8f, rampLength) * Mathf.Rad2Deg;
            rampObj.transform.rotation = Quaternion.Euler(-rampAngle, 0, 0);

            var rampBox = rampObj.AddComponent<BoxCollider>();
            rampBox.size = new Vector3(2.4f, 0.2f, Mathf.Sqrt(rampLength * rampLength + 1.8f * 1.8f));

            var rampFilter = rampObj.AddComponent<MeshFilter>();
            var rampCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rampFilter.sharedMesh = rampCube.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(rampCube);

            var rampRend = rampObj.AddComponent<MeshRenderer>();
            rampRend.sharedMaterial = CampusMaterialPalette.PlazaStone;
            rampObj.transform.localScale = rampBox.size;

            CreateSignboard(furnishingsRoot, "Sign_Ramp", new Vector3(-2.5f, 2.2f, 41.8f), new Vector3(1.4f, 0.35f, 0.05f), "ACCESSIBLE RAMP\n[Step-Free Route]", 13);

            // Destination Sign on Terrace
            CreateSignboard(furnishingsRoot, "Sign_Terrace", new Vector3(0f, 3.2f, 54.7f), new Vector3(2.8f, 0.5f, 0.05f), "OBSERVATION TERRACE\n& STUDY DECK", 16);
        }

        // ==========================================
        // 10. LIGHTING
        // ==========================================
        private void BuildLighting()
        {
            // Main directional sun light
            var sunObj = GameObject.Find("Directional Light") ?? new GameObject("Directional Light");
            sunObj.transform.SetParent(lightingRoot);
            sunObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var sun = sunObj.GetComponent<Light>();
            if (sun == null) sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.97f, 0.92f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;

            // Ambient interior fill lights
            CreatePointLight(lightingRoot, new Vector3(0f, 3.5f, 8.0f), 18f, 1.2f, new Color(1f, 0.95f, 0.88f));   // Lobby
            CreatePointLight(lightingRoot, new Vector3(0f, 3.5f, 24.0f), 16f, 1.0f, new Color(1f, 0.95f, 0.88f));  // Main Junction
            CreatePointLight(lightingRoot, new Vector3(0f, 3.5f, 36.0f), 16f, 1.0f, new Color(1f, 0.95f, 0.88f));  // North Spine
            CreatePointLight(lightingRoot, new Vector3(-16f, 3.5f, 18.0f), 14f, 1.0f, new Color(1f, 0.95f, 0.9f)); // Class 101
            CreatePointLight(lightingRoot, new Vector3(-16f, 3.5f, 30.0f), 14f, 1.0f, new Color(1f, 0.95f, 0.9f)); // Class 102
            CreatePointLight(lightingRoot, new Vector3(-16f, 3.5f, 44.0f), 18f, 1.2f, new Color(0.9f, 0.95f, 1f)); // Lab 204
            CreatePointLight(lightingRoot, new Vector3(16f, 3.5f, 39.0f), 18f, 1.1f, new Color(1f, 0.95f, 0.88f));  // Library
            CreatePointLight(lightingRoot, new Vector3(16f, 3.5f, 19.0f), 16f, 1.1f, new Color(1f, 0.95f, 0.88f));  // Canteen
        }

        // ==========================================
        // HELPER BUILDERS & PROPS
        // ==========================================
        private void CreateWallWithDoorway(Transform parent, string name, Vector3 origin, float wallLength, float doorWidth, float doorHeight, bool alongZ)
        {
            float wallThickness = 0.3f;
            float totalHeight = 4.0f;
            float segmentLen = (wallLength - doorWidth) * 0.5f;

            var wallGroup = new GameObject(name);
            wallGroup.transform.SetParent(parent);

            if (alongZ)
            {
                // North-South wall running along Z axis
                // Side 1
                CreateSolidBox(wallGroup.transform, "Wall_Left",
                    origin + new Vector3(0, totalHeight * 0.5f, -(doorWidth * 0.5f + segmentLen * 0.5f)),
                    new Vector3(wallThickness, totalHeight, segmentLen),
                    CampusMaterialPalette.InteriorWall);

                // Side 2
                CreateSolidBox(wallGroup.transform, "Wall_Right",
                    origin + new Vector3(0, totalHeight * 0.5f, (doorWidth * 0.5f + segmentLen * 0.5f)),
                    new Vector3(wallThickness, totalHeight, segmentLen),
                    CampusMaterialPalette.InteriorWall);

                // Header above door opening
                float headerHeight = totalHeight - doorHeight;
                CreateSolidBox(wallGroup.transform, "Wall_Header",
                    origin + new Vector3(0, doorHeight + headerHeight * 0.5f, 0),
                    new Vector3(wallThickness, headerHeight, doorWidth),
                    CampusMaterialPalette.InteriorWall);

                // Finished doorframe jambs
                CreateSolidBox(wallGroup.transform, "Frame_Jamb_L",
                    origin + new Vector3(0, doorHeight * 0.5f, -doorWidth * 0.5f),
                    new Vector3(wallThickness * 1.1f, doorHeight, 0.08f),
                    CampusMaterialPalette.DarkMetal);

                CreateSolidBox(wallGroup.transform, "Frame_Jamb_R",
                    origin + new Vector3(0, doorHeight * 0.5f, doorWidth * 0.5f),
                    new Vector3(wallThickness * 1.1f, doorHeight, 0.08f),
                    CampusMaterialPalette.DarkMetal);
            }
            else
            {
                // East-West wall running along X axis
                CreateSolidBox(wallGroup.transform, "Wall_Left",
                    origin + new Vector3(-(doorWidth * 0.5f + segmentLen * 0.5f), totalHeight * 0.5f, 0),
                    new Vector3(segmentLen, totalHeight, wallThickness),
                    CampusMaterialPalette.InteriorWall);

                CreateSolidBox(wallGroup.transform, "Wall_Right",
                    origin + new Vector3((doorWidth * 0.5f + segmentLen * 0.5f), totalHeight * 0.5f, 0),
                    new Vector3(segmentLen, totalHeight, wallThickness),
                    CampusMaterialPalette.InteriorWall);

                float headerHeight = totalHeight - doorHeight;
                CreateSolidBox(wallGroup.transform, "Wall_Header",
                    origin + new Vector3(0, doorHeight + headerHeight * 0.5f, 0),
                    new Vector3(doorWidth, headerHeight, wallThickness),
                    CampusMaterialPalette.InteriorWall);

                CreateSolidBox(wallGroup.transform, "Frame_Jamb_L",
                    origin + new Vector3(-doorWidth * 0.5f, doorHeight * 0.5f, 0),
                    new Vector3(0.08f, doorHeight, wallThickness * 1.1f),
                    CampusMaterialPalette.DarkMetal);

                CreateSolidBox(wallGroup.transform, "Frame_Jamb_R",
                    origin + new Vector3(doorWidth * 0.5f, doorHeight * 0.5f, 0),
                    new Vector3(0.08f, doorHeight, wallThickness * 1.1f),
                    CampusMaterialPalette.DarkMetal);
            }
        }

        private void CreateInteractiveHingedDoor(Transform parent, string name, Vector3 hingePosition, string roomName)
        {
            var doorRoot = new GameObject(name);
            doorRoot.transform.SetParent(parent);
            doorRoot.transform.position = hingePosition;

            var hinge = new GameObject("HingePivot");
            hinge.transform.SetParent(doorRoot.transform);
            hinge.transform.localPosition = Vector3.zero;

            // Door leaf: Width 1.35m, Height 2.35m, Thickness 0.06m
            // Pivot is at the hinge edge, so leaf center is offset by half width along Z (+0.675m)
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "DoorLeaf_SolidCollider";
            leaf.transform.SetParent(hinge.transform);
            leaf.transform.localPosition = new Vector3(0, 1.18f, 0.675f);
            leaf.transform.localScale = new Vector3(0.06f, 2.35f, 1.35f);

            var leafRend = leaf.GetComponent<Renderer>();
            if (leafRend != null) leafRend.sharedMaterial = CampusMaterialPalette.DoorLeafWood;

            // Door Handle
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "Handle";
            handle.transform.SetParent(leaf.transform);
            handle.transform.localPosition = new Vector3(0.6f, 0f, 0.4f);
            handle.transform.localRotation = Quaternion.Euler(0, 0, 90);
            handle.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            var handleCol = handle.GetComponent<Collider>();
            if (handleCol != null) DestroyImmediate(handleCol);
            var handleRend = handle.GetComponent<Renderer>();
            if (handleRend != null) handleRend.sharedMaterial = CampusMaterialPalette.DarkMetal;

            var doorScript = doorRoot.AddComponent<InteractableDoor>();
            // Use reflection or serialized fields if private, but fields are public or serialized
            doorRoot.AddComponent<BoxCollider>().size = new Vector3(0.5f, 2.4f, 1.4f);
        }

        private void CreateSignboard(Transform parent, string name, Vector3 pos, Vector3 size, string text, int fontSize)
        {
            var signObj = new GameObject(name);
            signObj.transform.SetParent(parent);
            signObj.transform.position = pos;

            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "SignBack";
            back.transform.SetParent(signObj.transform, false);
            back.transform.localScale = size;
            var backCol = back.GetComponent<Collider>();
            if (backCol != null) DestroyImmediate(backCol);
            var backRend = back.GetComponent<Renderer>();
            if (backRend != null) backRend.sharedMaterial = CampusMaterialPalette.SignBoard;

            // Sign text
            var textCanvasObj = new GameObject("SignCanvas");
            textCanvasObj.transform.SetParent(signObj.transform, false);
            textCanvasObj.transform.localPosition = new Vector3(0, 0, -size.z * 0.55f);

            var c = textCanvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            var rt = textCanvasObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size.x * 200f, size.y * 200f);
            rt.localScale = Vector3.one * 0.005f;

            var txtObj = new GameObject("Text");
            txtObj.transform.SetParent(textCanvasObj.transform, false);
            var txtRt = txtObj.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            var txt = txtObj.AddComponent<UnityEngine.UI.Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
        }

        private void CreateComputerWorkstation(Transform parent, Vector3 deskPos, float rotY)
        {
            var station = new GameObject("Workstation");
            station.transform.SetParent(parent);
            station.transform.position = deskPos;
            station.transform.rotation = Quaternion.Euler(0, rotY, 0);

            // PC Tower
            CreateSolidBox(station.transform, "PC_Tower", new Vector3(0.35f, 0.22f, 0.1f), new Vector3(0.18f, 0.44f, 0.42f), CampusMaterialPalette.DarkMetal);

            // Flat Screen Monitor
            CreateSolidBox(station.transform, "Monitor_Base", new Vector3(0, 0.02f, 0), new Vector3(0.24f, 0.03f, 0.18f), CampusMaterialPalette.DarkMetal);
            CreateSolidBox(station.transform, "Monitor_Neck", new Vector3(0, 0.15f, -0.05f), new Vector3(0.04f, 0.25f, 0.04f), CampusMaterialPalette.DarkMetal);
            CreateSolidBox(station.transform, "Monitor_Display", new Vector3(0, 0.28f, 0), new Vector3(0.55f, 0.32f, 0.04f), CampusMaterialPalette.ComputerScreen);

            // Keyboard
            CreateSolidBox(station.transform, "Keyboard", new Vector3(0, 0.015f, -0.22f), new Vector3(0.44f, 0.02f, 0.14f), CampusMaterialPalette.DarkMetal);
        }

        private void CreateComputerBenchRow(Transform parent, Vector3 startPos, int workstationCount)
        {
            float benchLength = workstationCount * 1.6f;
            CreateSolidBox(parent, "Lab_Bench", startPos + new Vector3(0, 0.4f, benchLength * 0.5f), new Vector3(1.1f, 0.8f, benchLength), CampusMaterialPalette.OakWood);

            for (int i = 0; i < workstationCount; i++)
            {
                Vector3 pos = startPos + new Vector3(0, 0.8f, (i + 0.5f) * 1.6f);
                CreateComputerWorkstation(parent, pos, 90f);

                // Chair
                CreateChair(parent, pos + new Vector3(0.7f, -0.8f, 0), -90f);
            }
        }

        private void CreateServerRack(Transform parent, Vector3 pos)
        {
            var rack = new GameObject("ServerRack");
            rack.transform.SetParent(parent);
            rack.transform.position = pos;

            // Tall dark steel cabinet
            CreateSolidBox(rack.transform, "Rack_Frame", new Vector3(0, 1.1f, 0), new Vector3(0.8f, 2.2f, 0.8f), CampusMaterialPalette.DarkMetal);

            // Front server units with LED glow
            for (int u = 0; u < 6; u++)
            {
                CreateSolidBox(rack.transform, $"Unit_{u}", new Vector3(0, 0.4f + u * 0.28f, 0.38f), new Vector3(0.68f, 0.18f, 0.05f), CampusMaterialPalette.SignBoard);
            }
        }

        private void CreateTallBookcase(Transform parent, Vector3 pos, float rotY)
        {
            var bookcase = new GameObject("TallBookcase");
            bookcase.transform.SetParent(parent);
            bookcase.transform.position = pos;
            bookcase.transform.rotation = Quaternion.Euler(0, rotY, 0);

            // Oak frame: Width 2.4m, Height 2.8m, Depth 0.45m
            CreateSolidBox(bookcase.transform, "Frame", new Vector3(0, 1.4f, 0), new Vector3(2.4f, 2.8f, 0.45f), CampusMaterialPalette.OakWood);

            // Shelves with colored books
            for (int s = 0; s < 4; s++)
            {
                CreateSolidBox(bookcase.transform, $"Books_{s}", new Vector3(0, 0.55f + s * 0.65f, 0), new Vector3(2.2f, 0.4f, 0.38f), CampusMaterialPalette.BookBlock);
            }
        }

        private void CreateStudyCarrel(Transform parent, Vector3 pos)
        {
            var carrel = new GameObject("StudyCarrel");
            carrel.transform.SetParent(parent);
            carrel.transform.position = pos;

            CreateSolidBox(carrel.transform, "CarrelDesk", new Vector3(0, 0.4f, 0), new Vector3(1.2f, 0.75f, 0.8f), CampusMaterialPalette.OakWood);
            CreateSolidBox(carrel.transform, "Divider_Back", new Vector3(0, 0.85f, 0.38f), new Vector3(1.2f, 0.5f, 0.04f), CampusMaterialPalette.WallAccentTeal);
            CreateSolidBox(carrel.transform, "Divider_L", new Vector3(-0.58f, 0.85f, 0.1f), new Vector3(0.04f, 0.5f, 0.6f), CampusMaterialPalette.WallAccentTeal);
            CreateSolidBox(carrel.transform, "Divider_R", new Vector3(0.58f, 0.85f, 0.1f), new Vector3(0.04f, 0.5f, 0.6f), CampusMaterialPalette.WallAccentTeal);

            CreateChair(carrel.transform, pos + new Vector3(0, 0, -0.6f), 0f);
        }

        private void CreateDiningTableSet(Transform parent, Vector3 pos)
        {
            var set = new GameObject("DiningTableSet");
            set.transform.SetParent(parent);
            set.transform.position = pos;

            // Round or square dining table
            CreateSolidBox(set.transform, "TableTop", new Vector3(0, 0.72f, 0), new Vector3(1.2f, 0.05f, 1.2f), CampusMaterialPalette.FloorTile);
            CreateSolidBox(set.transform, "TableLeg", new Vector3(0, 0.35f, 0), new Vector3(0.12f, 0.7f, 0.12f), CampusMaterialPalette.DarkMetal);
            CreateSolidBox(set.transform, "TableBase", new Vector3(0, 0.02f, 0), new Vector3(0.6f, 0.04f, 0.6f), CampusMaterialPalette.DarkMetal);

            // 4 Chairs around table
            CreateChair(set.transform, pos + new Vector3(0, 0, -0.7f), 0f);
            CreateChair(set.transform, pos + new Vector3(0, 0, 0.7f), 180f);
            CreateChair(set.transform, pos + new Vector3(-0.7f, 0, 0), 90f);
            CreateChair(set.transform, pos + new Vector3(0.7f, 0, 0), -90f);
        }

        private void CreateStudentDeskPair(Transform parent, Vector3 pos, float rotY)
        {
            var pair = new GameObject("DeskPair");
            pair.transform.SetParent(parent);
            pair.transform.position = pos;
            pair.transform.rotation = Quaternion.Euler(0, rotY, 0);

            CreateSolidBox(pair.transform, "DeskTop", new Vector3(0, 0.72f, 0), new Vector3(1.4f, 0.04f, 0.65f), CampusMaterialPalette.OakWood);
            CreateSolidBox(pair.transform, "Legs", new Vector3(0, 0.35f, 0), new Vector3(1.3f, 0.7f, 0.55f), CampusMaterialPalette.DarkMetal);

            CreateChair(pair.transform, pos + new Vector3(-0.35f, 0, -0.55f), 0f);
            CreateChair(pair.transform, pos + new Vector3(0.35f, 0, -0.55f), 0f);
        }

        private void CreateChair(Transform parent, Vector3 pos, float rotY)
        {
            var chair = new GameObject("Chair");
            chair.transform.SetParent(parent);
            chair.transform.position = pos;
            chair.transform.rotation = Quaternion.Euler(0, rotY, 0);

            // Seat
            CreateSolidBox(chair.transform, "Seat", new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.05f, 0.42f), CampusMaterialPalette.DarkMetal);
            // Backrest
            CreateSolidBox(chair.transform, "Backrest", new Vector3(0, 0.75f, -0.18f), new Vector3(0.42f, 0.38f, 0.04f), CampusMaterialPalette.DarkMetal);
            // 4 legs
            CreateSolidBox(chair.transform, "Legs", new Vector3(0, 0.22f, 0), new Vector3(0.38f, 0.44f, 0.38f), CampusMaterialPalette.DarkMetal);
        }

        private void CreateSofa(Transform parent, Vector3 pos, float rotY)
        {
            var sofa = new GameObject("ModernSofa");
            sofa.transform.SetParent(parent);
            sofa.transform.position = pos;
            sofa.transform.rotation = Quaternion.Euler(0, rotY, 0);

            CreateSolidBox(sofa.transform, "Base", new Vector3(0, 0.35f, 0), new Vector3(2.0f, 0.45f, 0.85f), CampusMaterialPalette.WallAccentTeal);
            CreateSolidBox(sofa.transform, "Back", new Vector3(0, 0.65f, 0.35f), new Vector3(2.0f, 0.55f, 0.2f), CampusMaterialPalette.WallAccentTeal);
            CreateSolidBox(sofa.transform, "Arm_L", new Vector3(-0.95f, 0.5f, 0), new Vector3(0.2f, 0.45f, 0.85f), CampusMaterialPalette.WallAccentTeal);
            CreateSolidBox(sofa.transform, "Arm_R", new Vector3(0.95f, 0.5f, 0), new Vector3(0.2f, 0.45f, 0.85f), CampusMaterialPalette.WallAccentTeal);
        }

        private void CreateParkBench(Transform parent, Vector3 pos, float rotY)
        {
            var bench = new GameObject("ParkBench");
            bench.transform.SetParent(parent);
            bench.transform.position = pos;
            bench.transform.rotation = Quaternion.Euler(0, rotY, 0);

            CreateSolidBox(bench.transform, "Slats", new Vector3(0, 0.42f, 0), new Vector3(1.8f, 0.06f, 0.45f), CampusMaterialPalette.OakWood);
            CreateSolidBox(bench.transform, "Back", new Vector3(0, 0.75f, 0.2f), new Vector3(1.8f, 0.4f, 0.06f), CampusMaterialPalette.OakWood);
            CreateSolidBox(bench.transform, "Legs", new Vector3(0, 0.2f, 0), new Vector3(1.6f, 0.4f, 0.4f), CampusMaterialPalette.DarkMetal);
        }

        private void CreatePlanter(Transform parent, Vector3 pos)
        {
            var planter = new GameObject("Planter");
            planter.transform.SetParent(parent);
            planter.transform.position = pos;

            // Concrete box
            CreateSolidBox(planter.transform, "Box", new Vector3(0, 0.35f, 0), new Vector3(1.6f, 0.7f, 1.6f), CampusMaterialPalette.ArchConcrete);

            // Soil
            CreateSolidBox(planter.transform, "Soil", new Vector3(0, 0.65f, 0), new Vector3(1.4f, 0.1f, 1.4f), CampusMaterialPalette.LawnGrass);

            // Shrub / Ornamental Bush
            var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = "Bush";
            bush.transform.SetParent(planter.transform, false);
            bush.transform.localPosition = new Vector3(0, 1.1f, 0);
            bush.transform.localScale = new Vector3(1.2f, 1.0f, 1.2f);
            var bCol = bush.GetComponent<Collider>();
            if (bCol != null) DestroyImmediate(bCol);
            var bRend = bush.GetComponent<Renderer>();
            if (bRend != null) bRend.sharedMaterial = CampusMaterialPalette.LawnGrass;
        }

        private void CreateLampPost(Transform parent, Vector3 pos)
        {
            var lamp = new GameObject("LampPost");
            lamp.transform.SetParent(parent);
            lamp.transform.position = pos;

            // Steel post
            CreateSolidBox(lamp.transform, "Pole", new Vector3(0, 1.8f, 0), new Vector3(0.12f, 3.6f, 0.12f), CampusMaterialPalette.DarkMetal);

            // Lamp fixture head
            CreateSolidBox(lamp.transform, "Fixture", new Vector3(0, 3.6f, 0), new Vector3(0.4f, 0.3f, 0.4f), CampusMaterialPalette.DarkMetal);

            // Point light
            CreatePointLight(lamp.transform, new Vector3(0, 3.4f, 0), 10f, 0.8f, new Color(1.0f, 0.94f, 0.82f));
        }

        private void CreatePointLight(Transform parent, Vector3 pos, float range, float intensity, Color color)
        {
            var lightObj = new GameObject("PointLight");
            lightObj.transform.SetParent(parent);
            lightObj.transform.position = pos;

            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None; // Fast for VR
        }

        private GameObject CreateSolidBox(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = pos;
            box.transform.localScale = size;

            // Ensure solid BoxCollider is attached and configured
            var col = box.GetComponent<BoxCollider>();
            if (col == null) col = box.AddComponent<BoxCollider>();
            col.isTrigger = false;

            var rend = box.GetComponent<Renderer>();
            if (rend != null && mat != null)
            {
                rend.sharedMaterial = mat;
            }

            return box;
        }
    }
}
