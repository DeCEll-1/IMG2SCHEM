using ImageMagick;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace IMG2SCHEM
{
    internal class PreDefinedClusterShapes
    {

        public static readonly (int x, int y)[] validShapes = [(1, 1), (2, 1), (1, 2)];

        private static void PadImage(ref MagickImage image, double targetRatio)
        {
            uint width = image.Width;
            uint height = image.Height;
            double currentRatio = (double)width / height;

            uint newWidth = width;
            uint newHeight = height;

            if (currentRatio > targetRatio)
            {
                // Too wide → pad vertically
                newHeight = Convert.ToUInt32(Math.Ceiling(width / targetRatio));
            }
            else if (currentRatio < targetRatio)
            {
                // Too tall → pad horizontally
                newWidth = Convert.ToUInt32(Math.Ceiling(height * targetRatio));
            }

            // Transparent background (you can change this)
            image.BackgroundColor = MagickColors.Black;

            // Resize canvas (Extent pads with background color)
            image.Extent(newWidth, newHeight, Gravity.Center);
        }

        public static DisplayCluster Get2x1(MagickImage image)
        {
            PadImage(ref image, 2f / 1f);

            IReadOnlyList<IMagickImage<byte>> tiles = image.CropToTiles(image.Width / 2, image.Height);

            return new DisplayCluster()
            .Add(new(
                1, 0,
                new(tiles[0]),
                new List<(int x, int y)> {
                    (0,0),
                    (0,1),
                    (0,2),
                    (0,3),
                    (0,4),
                    (0,5),

                    (0,6),
                    (1,6),
                    (2,6),
                    (3,6),
                    (4,6),
                    (5,6),
                    (6,6),
                },
                debugFilePrefix: "2x1_0c0",
                i: 0))
            .Add(new(
                7, 0,
                new(tiles[1]),
                new List<(int x, int y)> {
                    (13,0),
                    (13,1),
                    (13,2),
                    (13,3),
                    (13,4),
                    (13,5),

                    (13,6),
                    (12,6),
                    (11,6),
                    (10,6),
                    (9,6),
                    (8,6),
                    (7,6),
                },
                debugFilePrefix: "2x1_1c0",
                i: 1))
            .CreateColorBlocks()
            .FillProcessorBlocks();
        }

        public static DisplayCluster Get1x1(MagickImage image)
        {
            PadImage(ref image, 1f / 1f);

            return new DisplayCluster().Add(new(
                1, 0,
                image,
                new List<(int x, int y)> {
                    (0,0),
                    (0,1),
                    (0,2),
                    (0,3),
                    (0,4),
                    (0,5),

                    (0,6),
                    (1,6),
                    (2,6),
                    (3,6),
                    (4,6),
                    (5,6),
                    (6,6),
                    (7,6),

                    (8,0),
                    (8,1),
                    (8,2),
                    (8,3),
                    (8,4),
                    (8,5),
                },
                debugFilePrefix: "1x1_0c0",
                i: 0))
            .CreateColorBlocks()
            .FillProcessorBlocks();
        }

        public static DisplayCluster Get1x2(MagickImage image)
        {
            // Pad the image to 1:2 ratio (width:height)
            PadImage(ref image, 1f / 2f);

            // Split into 1 column, 2 rows (vertical split)
            IReadOnlyList<IMagickImage<byte>> tiles = image.CropToTiles(image.Width, image.Height / 2);

            return new DisplayCluster()
                .Add(new(
                    1, 0,
                    new(tiles[0]),
                    new List<(int x, int y)>
                    {
                        (0,0),
                        (0,1),
                        (0,2),
                        (0,3),
                        (0,4),
                        (0,5),

                        (7,0),
                        (7,1),
                        (7,2),
                        (7,3),
                        (7,4),
                        (7,5),
                    },
                    debugFilePrefix: "1x2_0c0",
                    i: 0))
                .Add(new(
                    1, 6, // stacked below the first tile
                    new(tiles[1]),
                    new List<(int x, int y)>
                    {
                        (0,6),
                        (0,7),
                        (0,8),
                        (0,9),
                        (0,10),
                        (0,11),

                        (7,6),
                        (7,7),
                        (7,8),
                        (7,9),
                        (7,10),
                        (7,11),
                    },
                    debugFilePrefix: "1x2_0c1",
                    i: 1))
                .CreateColorBlocks()
                .FillProcessorBlocks();
        }


    }
}
