using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageToolApp
{
    public struct Vector2
    {
        public Vector2(double nx, double ny)
        {
            x = nx;
            y = ny;
        }
        public void mult(double s)
        {
            x *= s;
            y *= s;
        }

        public double x;
        public double y;
    };
}
