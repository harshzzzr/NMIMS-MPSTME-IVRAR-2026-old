using System;
using UnityEngine;

namespace SmartCampus.Navigation
{
    public enum CampusNodeType
    {
        Entrance,
        Reception,
        Classroom,
        ComputerLab,
        Library,
        Canteen,
        Corridor,
        Junction,
        Stairs,
        Ramp,
        Terrace
    }

    public enum DestinationCategory
    {
        All,
        Academic,
        Labs,
        Library,
        Amenities
    }

    [Serializable]
    public class CampusNode
    {
        public string nodeId;
        public string displayName;
        public Vector3 position;
        public CampusNodeType nodeType = CampusNodeType.Corridor;
        public DestinationCategory category = DestinationCategory.Academic;
        public bool isAccessible = true;
        public int floorLevel = 0;
        [TextArea(1, 3)]
        public string description;

        public CampusNode() { }

        public CampusNode(string id, string name, Vector3 pos, CampusNodeType type, DestinationCategory cat, bool accessible = true, int floor = 0, string desc = "")
        {
            nodeId = id;
            displayName = name;
            position = pos;
            nodeType = type;
            category = cat;
            isAccessible = accessible;
            floorLevel = floor;
            description = string.IsNullOrEmpty(desc) ? name : desc;
        }

        public override string ToString()
        {
            return $"{displayName} ({nodeId}) [{nodeType}] Floor {floorLevel} @ {position}";
        }
    }
}
