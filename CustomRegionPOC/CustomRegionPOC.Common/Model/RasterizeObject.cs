using System;
using System.Collections.Generic;
using System.Text;

namespace CustomRegionPOC.Common.Model
{
    public class RasterizeObject
    {
        public List<Tile> tiles { get; set; }

        public string intersectionPolygon { get; set; }
    }
}
