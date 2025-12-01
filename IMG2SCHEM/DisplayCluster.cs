using ImageMagick;
using MindustrySchematicCreator;
using System.Data.SqlTypes;

namespace IMG2SCHEM
{
    public class DisplayCluster
    {
        public List<DisplaySection> sections = new();
        public DisplayCluster Add(DisplaySection sec)
        {
            sections.Add(sec);
            return this;
        }
        public DisplayCluster CreateColorBlocks()
        {
            sections.ForEach(sec => sec.CreateColorBlocks());
            return this;
        }

        public DisplayCluster UpdateLegitProcessorCoords((int width, int height) size)
        {
            // this mainly cretes the filled array for proccessor coord calculation
            bool[,] filled = new bool[size.width, size.height];
            foreach (DisplaySection sec in sections)
            {
                int x = sec.DisplayBlock.xOffset;
                int y = sec.DisplayBlock.yOffset;

                for (int dy = 0; dy < 6; dy++)
                    for (int dx = 0; dx < 6; dx++)
                    {
                        int px = x + dx;
                        int py = y + dy;
                        if (px >= 0 && px < size.width && py >= 0 && py < size.height)
                            filled[px, py] = true;
                    }
            }

            _filled = filled;
            return this;
        }
        private static bool[,] _filled { get => DisplaySection._filled; set => DisplaySection._filled = value; }
        private static (int x, int y)[] getFilledCoordinates(bool[,] filled)
        {
            List<(int x, int y)> filledPositions = new();


            for (int x = 0; x < filled.GetLength(0); x++)
            {
                for (int y = 0; y < filled.GetLength(1); y++)
                {
                    if (filled[x, y])
                        filledPositions.Add((x, y));
                }
            }

            return filledPositions.ToArray();
        }
        public DisplayCluster FillProcessorBlocks()
        {


            (int x, int y)[] coords = getFilledCoordinates(_filled);

            double avgX = coords.Average(k => k.x);
            double avgY = coords.Average(k => k.y);

            (double x, double y) center = (avgX, avgY);

            sections.OrderBy(sec =>
                Math.Pow((sec.DisplayBlock.xOffset + 2 - center.y), 2) +
                Math.Pow((sec.DisplayBlock.yOffset + 2 - center.y), 2)
                )
                .ToList()
                .ForEach(sec => sec.FillProcessorBlocks());
            return this;
        }

        public DisplayCluster FillSchem(Schematic schem)
        {
            sections.ForEach(sec => sec.FillSchem(schem));
            return this;
        }


        private static (int w, int h) GetProcessorSize(int columns, int rows)
        => (columns * 6 + 8, rows * 6 + 8);
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

            // Black background
            image.BackgroundColor = MagickColors.Black;

            // Resize canvas (Extent pads with background color)
            image.Extent(newWidth, newHeight, Gravity.Center);
        }


        public static DisplayCluster BuildCluster(MagickImage image, int columns, int rows)
        {
            PadImage(ref image, (double)columns / rows);

            var tileWidth = (uint)Math.Ceiling(image.Width / (float)columns);
            var tileHeight = (uint)Math.Ceiling(image.Height / (float)rows);

            image.ColorType = ColorType.TrueColor;
            IReadOnlyList<IMagickImage<byte>> tiles = image.CropToTiles(tileWidth, tileHeight);

            var cluster = new DisplayCluster();
            int tileIndex = 0;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int x = col * 6 + 3;
                    int y = row * 6 + 3;

                    string name = $"{columns}x{rows}_{col}c{row}";

                    cluster.Add(new(x, y, new MagickImage(tiles[tileIndex]), name, i: tileIndex));
                    tileIndex++;
                }
            }

            var size = GetProcessorSize(columns, rows);

            return cluster
                    .CreateColorBlocks()
                    .UpdateLegitProcessorCoords(size)
                    .FillProcessorBlocks();
        }
    }
}
