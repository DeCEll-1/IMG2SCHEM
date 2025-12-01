using ImageMagick;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IMG2SCHEM
{

    struct ColorBlock
    {
        public int x, y, width, height;
        public Color col;

        public ColorBlock(int x, int y, int width, int height, Color col)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.col = col;
        }

        public override string ToString()
        {
            return $"{x}, {y} : {width}, {height}; {col.ToString()}";
        }
    }
    struct Color
    {
        public byte r, g, b, a;

        public Color(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static bool operator ==(Color col1, Color col2)
        {
            if (
                col1.r == col2.r &&
                col1.g == col2.g &&
                col1.b == col2.b &&
                col1.a == col2.a
                )
                return true;
            return false;
        }
        public static bool operator !=(Color col1, Color col2)
        {
            if (
                col1.r == col2.r &&
                col1.g == col2.g &&
                col1.b == col2.b &&
                col1.a == col2.a
                )
                return false;
            return true;
        }

        public static Color FromArray(IPixel<byte> arr, uint channels)
        {
            switch (channels)
            {
                case 2:
                    //byte c = (byte)(arr[0] * (arr[1] / 255d));
                    byte c = arr[0];
                    return new Color(c, c, c, 255);
                case 3:
                case 4:
                    byte r = arr[0];
                    byte g = arr[1];
                    byte b = arr[2];
                    byte a = 255;
                    return new Color(r, g, b, a);
                default:
                    return new Color(255, 255, 255, 255);
            }
        }

        public override string ToString()
        {
            return $"{r},{g},{b},{a}";
        }
    }
}
