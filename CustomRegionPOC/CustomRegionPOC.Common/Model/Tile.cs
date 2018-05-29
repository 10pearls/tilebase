using System;
using System.Collections.Generic;
using System.Text;

namespace CustomRegionPOC.Common.Model
{
    public class Tile
    {
        public int Zoom { get; set; }
        public float Lat { get; set; }
        public float Lng { get; set; }

        public float Row { get; set; }
        public float Column { get; set; }

        public double Bound1 { get; set; }
        public double Bound2 { get; set; }
        public double Bound3 { get; set; }
        public double Bound4 { get; set; }

        public bool IsPartialTiles { get; set; }
    }
}