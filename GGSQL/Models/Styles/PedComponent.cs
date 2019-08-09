namespace GGSQL.Models.Styles
{
    public class PedComponent
    {
        public int ComponentId { get; set; }

        public int DrawableId { get; set; }

        public int TextureId { get; set; }

        public int PaletteId { get; set; }

        public PedComponent(int componentId, int drawableId, int textureId, int paletteId)
        {
            ComponentId = componentId;
            DrawableId = drawableId;
            TextureId = textureId;
            PaletteId = paletteId;
        }

        //public int Face { get; set; } // 0
        //public int Head { get; set; } // 1
        //public int Hair { get; set; } // 2
        //public int Torso { get; set;} // 3
        //public int Legs { get; set; } // 4
        //public int Hands { get; set; } // 5
        //public int Feet { get; set; } // 6
        //public int Eyes { get; set; } // 7
        //public int Accessoires { get; set; } // 8
        //public int Tasks { get; set; } // 9
        //public int Textures { get; set; } // 10
        //public int Torso2 { get; set; } // 11

    }
}
