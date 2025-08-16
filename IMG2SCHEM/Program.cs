using CommandLine;
using ImageMagick;
using ImageMagick.Colors;
using MindustryChematicCreator;
using MindustryChematicCreator.Configs;
using MindustrySchematicCreator;
using System.Diagnostics;
using System.Drawing;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Channels;
using TextCopy;

namespace IMG2SCHEM
{
    internal class Program
    {
        class Options
        {
            [Option('i', "input", Required = true, HelpText = "Input file to be processed.")]
            public string InputFile { get; set; }
            [Option('o', "output", Required = false, HelpText = "Output path to place the schematic base64.")]
            public string OutputFile { get; set; }

            [Option('v', "verbose", Required = false, HelpText = "Detailed outputs.")]
            public bool Verbose { get; set; }
        }
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
                byte r = arr[0];
                byte g = arr[1];
                byte b = arr[2];
                byte a = 255;

                return new Color(r, g, b, a);
            }

            public override string ToString()
            {
                return $"{r},{g},{b},{a}";
            }
        }

        static void Main(string[] args)
        {
            Options options = Parser.Default.ParseArguments<Options>(args).WithNotParsed<Options>(opts => { throw new ArgumentNullException("CLI Arguments not filled"); }).Value;

            MagickImage image = new MagickImage(File.ReadAllBytes(options.InputFile));
            image.Flip();

            // 176
            (uint imgResizeWidth, uint imgResizeHeight) = (176, 176);
            (float scaleRatioX, float scaleRatioY) = (176f / imgResizeWidth, 176f / imgResizeHeight);

            MagickGeometry geometry = new(imgResizeWidth, imgResizeHeight);
            geometry.IgnoreAspectRatio = true;
            image.Resize(geometry);

            QuantizeSettings settings = new();

            settings.Colors = 32;
            settings.DitherMethod = DitherMethod.FloydSteinberg;

            image.Quantize(settings);

            image.WriteAsync(new FileInfo(@"D:\Github\IMG2SCHEM\IMG2SCHEM\out.jpg"));



            IPixelCollection<byte> pixels = image.GetPixels();


            uint width = image.Width;
            uint height = image.Height;
            uint channels = image.ChannelCount;
            List<ColorBlock> blocks = new List<ColorBlock>();

            bool[,] visited = new bool[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (visited[x, y])
                        continue;

                    IPixel<byte> pixel = pixels.GetPixel(x, y);

                    Color pixelCol = Color.FromArray(pixel, channels);

                    int runW = 1;

                    while
                        (
                            x + runW < width &&
                            !visited[x + runW, y] &&
                            Color.FromArray(pixels.GetPixel(x + runW, y), channels) == pixelCol
                        )
                        runW++; // select all the horizontal pixels that are the same color


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


                    blocks.Add(new ColorBlock(x, y, runW, runH, pixelCol));
                }
            }



            Schematic schem = new();

            Block display = new(BlockData.Blocks[BlockType.large_logic_display], 1, 0);
            schem.AddBlock(display);

            LogicCodeConfig code = new LogicCodeConfig();
            StringBuilder sb = new();

            blocks.Sort((a, b) =>
            {
                int colorA = (a.col.r << 24) | (a.col.g << 16) | (a.col.b << 8) | a.col.a;
                int colorB = (b.col.r << 24) | (b.col.g << 16) | (b.col.b << 8) | b.col.a;
                return colorA.CompareTo(colorB);
            });

            Color? lastColor = blocks[0].col;
            int blockOffset = 0;
            int newlineCount = 0;
            float waitTime = 9f;
            string outCode = "";
            // adds the flush command
            void flush()
            { sb.AppendLine($"drawflush display1"); newlineCount++; }
            // adds the stop command
            void stop()
            { sb.AppendLine($"stop"); newlineCount++; }

            // adds the logic block to the schematic
            void addLogic()
            {
                Block logicBlock = new(BlockData.Blocks[BlockType.micro_processor], 0, blockOffset); // create the block

                LogicCodeConfig clonedConfig = (LogicCodeConfig)code.Clone(); // clone the config
                clonedConfig.Code = outCode.Remove(outCode.Length - 2); // put the code with last newline and carrier removed
                clonedConfig.Links.Add(new(display, logicBlock)); // add the display to the list
                logicBlock.config = clonedConfig; // update the blocks config
                schem.AddBlock(logicBlock); // add the block to schem
            }
            // updates the code string and clears the sb
            void clear()
            {
                outCode = sb.ToString();

                sb.Clear(); // clear the code for the next commands
                newlineCount = 0; // reset the line counter
            }

            for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                ColorBlock drawBlock = blocks[blockIndex];

                void drawCol()
                { sb.AppendLine($"draw color {drawBlock.col.r} {drawBlock.col.g} {drawBlock.col.b} {drawBlock.col.a} 0 0"); newlineCount++; }
                void wait()
                { sb.AppendLine($"wait {waitTime}"); newlineCount++; }

                if (lastColor != drawBlock.col || !sb.ToString().Contains("draw color"))
                {
                    lastColor = drawBlock.col;
                    flush();
                    drawCol();
                }

                sb.AppendLine(
                    $"draw rect " +
                    $"{Math.Ceiling(drawBlock.x * scaleRatioX)} " +
                    $"{Math.Ceiling(drawBlock.y * scaleRatioY)} " +
                    $"{Math.Ceiling(drawBlock.width * scaleRatioX)} " +
                    $"{Math.Ceiling(drawBlock.height * scaleRatioY)} " +
                    $"0 0");
                newlineCount++;


                if (newlineCount % 100 == 0)
                    flush();

                // filter so we dont pass the insturction limit
                if (newlineCount >= 995)
                {
                    flush(); // draw everything left
                    stop();  // stop the logic from continuing work

                    // get all the code
                    clear(); // clear the sb and update the code string
                    addLogic(); // add the logic block

                    wait(); // add wait so the next drawing happens when this one finishes
                    waitTime += 9f; // increase the wait time for each block so they dont conflict 

                    drawCol(); // add the draw color as this is the new block

                    blockOffset++; // Move to the next processor slot (vertical offset in schematic)
                }
            }

            flush();
            stop();
            clear();
            addLogic();


            List<Color> usedColors = new List<Color>();

            lastColor = null;
            foreach (var block in blocks)
            {
                if (lastColor == null || lastColor != block.col)
                {
                    usedColors.Add(block.col);
                    lastColor = block.col;
                }
            }

            string schemOut = schem.GetBase64();
            ClipboardService.SetText(schemOut);
            Console.WriteLine();
            Console.WriteLine(blocks.Count);
            Console.WriteLine(schem.Blocks.Count);
            usedColors.ForEach(s => Console.WriteLine(s.ToString()));

            Schematic k = Schematic.FromBase64("bXNjaAF4nGPgYGBnZmDJS8xNZWApSc0tYOBOSS1OLsosKMnMz2NgYGDLSUxKzSlmYIqOZWJgTinNZ+DPzUwuytctKMpPTi0uzi8CKmJmgABGBnYGVj4gQ6piTnJKA4Mb27qmRX+eXHn06I+Ss4A8g0YFizxYERtIETdYERMDgxADEwMAKU4g6w==");

            string s = "";


        }

    }
}
