using CommandLine;
using CommandLine.Text;
using ImageMagick;
using ImageMagick.Colors;
using MindustryChematicCreator;
using MindustryChematicCreator.Configs;
using MindustrySchematicCreator;
using System.Diagnostics;
using System.Drawing;
using System.IO.Pipelines;
using System.Net.Security;
using System.Text;
using System.Threading.Channels;
using TextCopy;

namespace IMG2SCHEM
{
    internal class Program
    {
        class Options
        {
            #region file io

            [Option('i', "input", Required = true, HelpText = "Input image to be processed.")]
            public string InputFile { get; set; } = "";

            [Option('o', "output", Required = false, HelpText = "Output path to place the schematic base64.")]
            public string? OutputFile { get; set; }
            public bool OutputToFile => !string.IsNullOrWhiteSpace(OutputFile);

            #endregion

            #region image settings

            [Option(shortName: 'r', longName: "image-resolution", Required = false, HelpText = "The resolution to resize the input image to.", Default = (uint)176)]
            public uint ImageResolution { get; set; }

            [Option(shortName: 'c', longName: "color-amount", Required = false, HelpText = "The amount of colors to be used.", Default = (uint)32)]
            public uint ColorAmount { get; set; }

            [Option(shortName: 'd', longName: "dittering-method", Required = false, HelpText = "The dittering method to be used", Default = DitherMethod.No)]
            public DitherMethod ImageDitherMethod { get; set; }

            #endregion

            #region debug settings

            [Option('v', "verbose", Required = false, HelpText = "Detailed outputs.")]
            public bool Verbose { get; set; }

            [Option("debug", Required = false, HelpText = "Debug mode.")]
            public bool Debug { get; set; } = false;

            #endregion

            #region y/n

            [Option('y', "yes", Required = false, HelpText = "Override output files.")]
            public bool OverrideExistingFile { get; set; } = false;

            [Option('n', "no", Required = false, HelpText = "Never override output files.")]
            public bool DoNotOverrideExistingFile { get; set; } = false;

            #endregion

            #region output settings

            [Option('p', "clipboard", Required = false, HelpText = "Pastes the output schematic to clipboard.", Default = false)]
            public bool PasteToClipBoard { get; set; }

            [Option("print-output", Required = false, HelpText = "Print output schematic Base64 to console.")]
            public bool PrintOutput { get; set; } = false;

            #endregion

            #region schematic settings

            [Option("name", Required = false, HelpText = "Name of the output schematic.", Default = "Unnamed")]
            public string SchematicName { get; set; } = "Unnamed";

            #endregion
        }

        static void Main(string[] args)
        {
            var parser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = true;
                with.AutoHelp = true;
                with.AutoVersion = true;
            });

            parser.ParseArguments<Options>(args)
                .WithParsed(options =>
                {
                    if (AreOptionsValid(options))
                        Run(options);


                })
                .WithNotParsed(errors =>
                {
                    var helpText = HelpText.AutoBuild
                    (
                            parser.ParseArguments<Options>(args),
                            h =>
                            {
                                h.AutoVersion = true;
                                h.AutoHelp = true;
                                h.AddEnumValuesToHelpText = true;
                                return h;
                            }
                    );
                    Console.WriteLine(helpText);
                });
        }
        static bool AreOptionsValid(Options options)
        {
            if (options.OverrideExistingFile && options.DoNotOverrideExistingFile)
            {
                Console.WriteLine("Error: Cannot use both -y and -n options together.");
                return false;
            }
            if (!File.Exists(options.InputFile))
            {
                Console.WriteLine($"Error: '{options.InputFile}' cannot be found.");
                return false;
            }
            if (options.OutputToFile && !Path.IsPathRooted(options.OutputFile))
            {
                Console.WriteLine($"Error: '{options.OutputFile}' is not a valid output path.");
                return false;
            }
            if (options.ImageResolution > 176)
            {
                Console.WriteLine("Error: Image resolution cannot exceed 176.");
                return false;

            }
            if (options.ColorAmount is < 2 or > 256)
            {
                Console.WriteLine("Error: Color amount must be between 2 and 256.");
                return false;
            }

            if (!options.OutputToFile && !options.PasteToClipBoard && !options.PrintOutput)
            {
                if (PromptYesNo("No output specified. Save to file?"))
                    options.OutputFile = Path.ChangeExtension(options.InputFile, ".schem");
                else
                    return false;
            }
            return true;
        }
        static void Run(Options options)
        {
            MagickImage image = new MagickImage(File.ReadAllBytes(options.InputFile));
            image.Flip();

            uint imgRes = options.ImageResolution;

            (float scaleRatioX, float scaleRatioY) = (176f / imgRes, 176f / imgRes);

            MagickGeometry geometry = new(imgRes, imgRes);
            geometry.IgnoreAspectRatio = true;
            image.Resize(geometry);

            QuantizeSettings settings = new();

            settings.Colors = options.ColorAmount;
            settings.DitherMethod = options.ImageDitherMethod;

            image.Quantize(settings);

            if (options.Debug)
            {
                string debugImageOutPath = Path.GetTempPath() + Guid.NewGuid().ToString() + ".jpg";
                image.Write(new FileInfo(debugImageOutPath));
                Console.WriteLine("Wrote proccessed image into: " + debugImageOutPath);
            }


            #region color block creation
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
            #endregion


            Schematic schem = new();
            schem.Name = options.SchematicName;

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
            string outCode = "";

            #region code creation functions
            // adds the flush command
            void flush()
            { sb.AppendLine($"drawflush display1"); newlineCount++; }

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
            #endregion

            #region code creation
            for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                ColorBlock drawBlock = blocks[blockIndex];

                void drawCol()
                { sb.AppendLine($"draw color {drawBlock.col.r} {drawBlock.col.g} {drawBlock.col.b} {drawBlock.col.a} 0 0"); newlineCount++; }

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
                {
                    flush();
                    drawCol();
                }

                // filter so we dont pass the insturction limit
                if (newlineCount >= 995)
                {
                    flush(); // draw everything left

                    // get all the code
                    clear(); // clear the sb and update the code string
                    addLogic(); // add the logic block

                    drawCol(); // add the draw color as this is the new block

                    blockOffset++; // Move to the next processor slot (vertical offset in schematic)
                }
            }
            #endregion

            // be sure to finish up the last logic block
            flush();
            clear();
            addLogic();


            string schemOut = schem.GetBase64();

            if (options.OutputToFile)
            {
                if (File.Exists(options.OutputFile))
                {
                    if (options.DoNotOverrideExistingFile)
                        Console.WriteLine($"File '{options.OutputFile}' already exists.");
                    else if (options.OverrideExistingFile)
                        File.WriteAllText(options.OutputFile, schemOut);
                    else if (PromptYesNo($"File '{options.OutputFile}' already exists. Override?"))
                        File.WriteAllText(options.OutputFile, schemOut);
                }
                else
                    File.WriteAllText(options.OutputFile!, schemOut);
            }

            if (options.PasteToClipBoard)
                try { ClipboardService.SetText(schemOut); }
                catch (Exception ex) { Console.WriteLine($"Clipboard not supported: {ex.Message}"); }
            if (options.PrintOutput)
                Console.WriteLine(schemOut);

            ConfirmOutput(options, schem, blocks);
        }
        static void ConfirmOutput(Options options, Schematic schem, List<ColorBlock> blocks)
        {
            StringBuilder output = new();

            output.AppendLine($"Successfully created schematic: {options.SchematicName}");
            output.AppendLine($"Input: {options.InputFile}");
            output.AppendLine($"Output resolution: {options.ImageResolution}x{options.ImageResolution}");
            output.AppendLine($"Color amount: {options.ColorAmount}");
            output.AppendLine($"Proccessors used: {schem.Blocks.Where(s => s.data.Name.Contains("processor")).ToArray().Length}");
            output.AppendLine($"Total blocks: {schem.Blocks.Count}");
            if (options.OutputToFile)
                output.AppendLine($"Output: {options.OutputFile}");
            if (options.PasteToClipBoard)
                output.AppendLine($"Successfully pasted to clipboard.");
            if (options.Verbose)
            {
                output.AppendLine("v:");
                output.AppendLine("\t" + "Color block count: " + blocks.Count);
                output.AppendLine("\t" + "Used colors:");
                #region used colors creation
                List<Color> usedColors = new List<Color>();
                Color? lastColor = null;
                foreach (var block in blocks)
                {
                    if (lastColor == null || lastColor != block.col)
                    {
                        usedColors.Add(block.col);
                        lastColor = block.col;
                    }
                }
                #endregion
                usedColors.ForEach(c => output.AppendLine($"\t\t#{c.r:X2}{c.g:X2}{c.b:X2}{c.a:X2}"));
            }

            Console.WriteLine(output.ToString());
        }
        static bool PromptYesNo(string message)
        {
            while (true)
            {
                Console.Write($"{message} (y/n): ");
                ConsoleKey key = Console.ReadKey(intercept: true).Key;
                Console.WriteLine();

                switch (key)
                {
                    case ConsoleKey.Y: return true;
                    case ConsoleKey.N: return false;
                    default: continue;
                }
            ;
            }
        }
    }
}
