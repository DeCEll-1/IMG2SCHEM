using ImageMagick;
using MindustryChematicCreator;
using MindustryChematicCreator.Configs;
using MindustrySchematicCreator;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace IMG2SCHEM
{
    public class DisplaySection
    {
        private MagickImage Image { get; set; }
        public Block DisplayBlock { get; set; }
        private List<Block> ProcessorBlocks { get; set; } = new();

        private List<(int x, int y)> LegitProcessorCoords = new();
        private List<ColorBlock> ColorBlocks { get; set; } = new List<ColorBlock>();
        private int i;
        private uint ImageResolution => Program.options.ImageResolutions
            .ToArray()[i % Program.options.ImageResolutions.Count()];

        private uint ColorAmount => Program.options.ImageColorAmounts
            .ToArray()[i % Program.options.ImageColorAmounts.Count()];

        private DitherMethod DitherMethod => Program.options.ImageDitherMethods
            .ToArray()[i % Program.options.ImageDitherMethods.Count()];
        private bool Debug => Program.options.Debug;
        private string debugFilePrefix = "";

        public DisplaySection(int displayBlockX, int displayBlockY, MagickImage image, string debugFilePrefix = "", BlockType blockType = BlockType.large_logic_display, int i = 0)
        {
            DisplayBlock = new(BlockData.Blocks[blockType], displayBlockX, displayBlockY);
            Image = image;
            //LegitProcessorCoords = legitProcessorCoords;
            this.debugFilePrefix = debugFilePrefix;
            this.i = i;
        }
        public static bool[,] _filled { get; set; } // well its not like we gonna run this app more than once so who cares?

        private void AddProcessorSlot() // todo: add distance check for connection issues
            => LegitProcessorCoords.Add(BFSNonFilledCoordinate((DisplayBlock.xOffset + 2, DisplayBlock.yOffset + 2)));

        public (int x, int y) BFSNonFilledCoordinate((int x, int y) root)
        {
            int width = _filled.GetLength(0);
            int height = _filled.GetLength(1);
            var visited = new bool[width, height];
            Queue<(int x, int y)> que = [];
            que.Enqueue(root);
            visited[root.x, root.y] = true;
            while (que.Count != 0)
            {
                (int x, int y) tile = que.Dequeue();
                if (!_filled[tile.x, tile.y])
                { // if the file isnt filled
                    _filled[tile.x, tile.y] = true;
                    return tile;
                }
                else
                { // if the tile is filled
                    foreach ((int x, int y) t in new (int x, int y)[] {
                        (tile.x + 1, tile.y),
                        (tile.x - 1, tile.y),
                        (tile.x, tile.y + 1),
                        (tile.x, tile.y - 1),
                    })
                    {
                        if (
                            t.x >= 0 && t.x < width &&
                            t.y >= 0 && t.y < height &&
                            !visited[t.x, t.y]
                        )
                        {
                            visited[t.x, t.y] = true;
                            que.Enqueue((t.x, t.y));
                        }
                    }
                }
            }
            return (-1, -1);
        }

        public void CreateColorBlocks()
        {
            // Resize and quantize image
            Image.Resize(new MagickGeometry(ImageResolution, ImageResolution) { IgnoreAspectRatio = true });
            Image.Quantize(new QuantizeSettings { Colors = this.ColorAmount, DitherMethod = this.DitherMethod });


            if (Debug)
            {
                string debugPath = $"{Path.GetTempPath()}{debugFilePrefix}_display_{DisplayBlock.xOffset}_{DisplayBlock.yOffset}_{Guid.NewGuid()}.jpg";
                Image.Write(new FileInfo(debugPath));
                Console.WriteLine($"Wrote processed image for display at ({DisplayBlock.xOffset}, {DisplayBlock.yOffset}) to: {debugPath}");
            }

            // Generate color blocks
            IPixelCollection<byte> pixels = Image.GetPixels();
            uint width = Image.Width;
            uint height = Image.Height;
            uint channels = Image.ChannelCount;
            bool[,] visited = new bool[width, height];
            ColorBlocks.Clear();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (visited[x, y]) continue;

                    IPixel<byte> pixel = pixels.GetPixel(x, y);
                    Color pixelCol = Color.FromArray(pixel, channels);

                    int runW = 1;
                    while (x + runW < width && !visited[x + runW, y] &&
                           Color.FromArray(pixels.GetPixel(x + runW, y), channels) == pixelCol)
                        runW++;

                    int runH = 1;
                    bool done = false;
                    while (y + runH < height && !done)
                    {
                        for (int dx = 0; dx < runW; dx++)
                        {
                            if (visited[x + dx, y + runH] || Color.FromArray(pixels.GetPixel(x + dx, y + runH), channels) != pixelCol)
                            {
                                done = true;
                                break;
                            }
                        }
                        if (!done) runH++;
                    }

                    for (int yy = y; yy < y + runH; yy++)
                        for (int xx = x; xx < x + runW; xx++)
                            visited[xx, yy] = true;

                    ColorBlocks.Add(new ColorBlock(x, y, runW, runH, pixelCol));
                }
            }

            // Sort blocks by color for efficient code generation
            ColorBlocks.Sort((a, b) =>
            {
                int colorA = (a.col.r << 24) | (a.col.g << 16) | (a.col.b << 8) | a.col.a;
                int colorB = (b.col.r << 24) | (b.col.g << 16) | (b.col.b << 8) | b.col.a;
                return colorA.CompareTo(colorB);
            });
        }

        public void FillProcessorBlocks()
        {

            LogicCodeConfig code = new LogicCodeConfig();
            StringBuilder sb = new StringBuilder();
            Color? lastColor = ColorBlocks.Count > 0 ? ColorBlocks[0].col : null;
            int newlineCount = 0;
            string outCode = "";
            float scaleRatio = 176f / ImageResolution;
            int coordIndex = 0;

            void Flush()
            {
                sb.AppendLine($"drawflush display1");
                newlineCount++;
            }

            void DrawCol(ColorBlock block)
            {
                sb.AppendLine($"draw color {block.col.r} {block.col.g} {block.col.b} {block.col.a} 0 0");
                newlineCount++;
            }

            void AddLogic()
            {
                AddProcessorSlot();
                if (coordIndex >= LegitProcessorCoords.Count())
                {
                    Console.WriteLine($"Warning: Exceeded processor coordinates for display at ({DisplayBlock.xOffset},{DisplayBlock.yOffset}).");
                    return;
                }
                var (x, y) = LegitProcessorCoords.ElementAt(coordIndex);
                var logicBlock = new Block(BlockData.Blocks[BlockType.micro_processor], x, y);
                var clonedConfig = (LogicCodeConfig)code.Clone();
                clonedConfig.Code = outCode.Remove(outCode.Length - 2);
                clonedConfig.Links.Add(new(DisplayBlock, logicBlock));
                logicBlock.config = clonedConfig;
                ProcessorBlocks.Add(logicBlock);
                coordIndex++;
            }

            void Clear()
            {
                outCode = sb.ToString();
                sb.Clear();
                newlineCount = 0;
            }

            for (int i = 0; i < ColorBlocks.Count; i++)
            {
                var block = ColorBlocks[i];

                if (lastColor != block.col || !sb.ToString().Contains("draw color"))
                {
                    lastColor = block.col;
                    Flush();
                    DrawCol(block);
                }

                sb.AppendLine(
                    $"draw rect " +
                    $"{Math.Ceiling(block.x * scaleRatio)} " +
                    $"{Math.Ceiling(block.y * scaleRatio)} " +
                    $"{Math.Ceiling(block.width * scaleRatio)} " +
                    $"{Math.Ceiling(block.height * scaleRatio)} " +
                    $"0 0");
                newlineCount++;

                if (newlineCount % 100 == 0)
                {
                    Flush();
                    DrawCol(block);
                }

                if (newlineCount >= 990)
                {
                    Flush();
                    Clear();
                    AddLogic();
                    DrawCol(block);
                }
            }

            if (newlineCount > 0)
            {
                Flush();
                Clear();
                AddLogic();
            }
        }

        public void FillSchem(Schematic schem)
        {
            schem.AddBlock(DisplayBlock);
            ProcessorBlocks.ForEach(block => schem.AddBlock(block));
        }
    }
}
