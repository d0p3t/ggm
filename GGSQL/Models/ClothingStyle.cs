using System.Collections.Generic;
using GGSQL.Models.Styles;

namespace GGSQL.Models
{
    public class ClothingStyle : Style
    {
        public int Id { get; set; }
        public int SlotId { get; set; }

        public string ModelName { get; set; } = "mp_m_freemode_01";

        public List<PedComponent> PedComponents { get; set; } = new List<PedComponent>
        {
            new PedComponent(1, 57, 0, 2), // Head
            new PedComponent(3, 41, 0, 2), // Torso
            new PedComponent(4, 98, 13, 2), // Legs
            new PedComponent(6, 71, 13, 2), // Feet
            new PedComponent(8, 15, 0, 2), // Accessoires
            new PedComponent(11, 251, 13, 2) // Torso2
        };

        public void ResetToDefault()
        {
            PedComponents = new List<PedComponent>
            {
                new PedComponent(1, 57, 0, 2), // Head
                new PedComponent(3, 41, 0, 2), // Torso
                new PedComponent(4, 98, 13, 2), // Legs
                new PedComponent(6, 71, 13, 2), // Feet
                new PedComponent(8, 15, 0, 2), // Accessoires
                new PedComponent(11, 251, 13, 2) // Torso2
            };
        }

        public ClothingStyle(int slotId, bool isFirstStyle = false)
        {
            SlotId = slotId;
            IsActiveStyle = isFirstStyle;
        }
    }
}
