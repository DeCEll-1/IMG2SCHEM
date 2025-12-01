using CommandLine;
using CommandLine.Text;
using ImageMagick;
using MindustrySchematicCreator;
using System.Text;
using TextCopy;

namespace IMG2SCHEM
{
    internal class Program
    {
        public readonly record struct DisplayShape(int Width, int Height)
        {
            public override string ToString() => $"{Width}x{Height}";

            public static bool TryParse(string value, out DisplayShape shape)
            {
                shape = default;

                if (string.IsNullOrWhiteSpace(value))
                    return false;

                var parts = value.Split('x', 'X');
                if (parts.Length != 2)
                    return false;

                if (int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
                {
                    shape = new DisplayShape(w, h);
                    return true;
                }

                return false;
            }
        }

        public class Options
        {
            #region file io

            [Option('i', "input", Required = true, HelpText = "Input image to be processed.")]
            public string InputFile { get; set; } = "";

            [Option('o', "output", Required = false, HelpText = "Output path to place the schematic base64.")]
            public string? OutputFile { get; set; }
            public bool OutputToFile => !string.IsNullOrWhiteSpace(OutputFile);

            #endregion

            #region image settings

            [Option('r', "image-resolution", Required = false, HelpText =
                "(Default: 176) The resolution(s) to resize the input image to for a display. " +
                "176 is the max resolution, 88 will render half resolution etc. etc. \r\n" +
                "Multiple space-separated values (e.g. 44 88 176) " +
                "will cycle through the list for each display tile (from left to right top to bottom)"
                )]
            public IEnumerable<uint> ImageResolutions { get; set; } = [];

            [Option('c', "color-amount", Required = false, HelpText =
                "(Default: 16) Number of distinct colors used for displays. " +
                "Use a single value (e.g. 96) for consistent quality across the entire image.\r\n" +
                "Multiple space-separated values (e.g. 8 16 32) " +
                "will cycle through the list for each display tile (from left to right top to bottom)\r\n" +
                "/!\\ Not suggested to input more than 48 as it will increase the amount of processors significantly.\r\n" +
                "[!] This is a good setting to reduce if theres too many processor usage."
                )]
            public IEnumerable<uint> ImageColorAmounts { get; set; } = [];

            [Option('d', "dithering-method", Required = false, HelpText =
                "(Default: No) Dithering algorithm(s) to reduce color banding. " +
                "Available: No, Riemersma, FloydSteinberg.\r\n" +
                "Multiple space-separated values (e.g. No Riemersma FloydSteinberg) " +
                "will cycle through the list for each display tile (from left to right top to bottom)"
                )]
            public IEnumerable<DitherMethod> ImageDitherMethods { get; set; } = [];

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

            [Option(shortName: 's', longName: "display-shape", Required = false, HelpText = "The shape of the resulting display (e.g. 1x1, 2x1, 4x2)", Default = "1x1")]
            public string DisplayShapeString { get; set; } = "1x1";

            public DisplayShape DisplayShape
            {
                get
                {
                    if (!DisplayShape.TryParse(DisplayShapeString, out var shape))
                        throw new ArgumentException($"Invalid display shape: {DisplayShapeString}");
                    return shape;
                }
            }

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
                    {
                        Program.options = options;
                        Run();
                    }


                })
                .WithNotParsed(errors =>
                {
                    var helpText = HelpText.AutoBuild
                    (
                            parser.ParseArguments<Options>(args),
                            h =>
                            {
                                h.MaximumDisplayWidth = 100;
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
            if (options.ImageResolutions.Any(x => x > 176))
            {
                Console.WriteLine("Error: Image resolution cannot exceed 176.");
                return false;

            }
            if (options.ImageColorAmounts.Any(x => x is < 2 or > 256))
            {
                Console.WriteLine("Error: Color amount must be between 2 and 256.");
                return false;
            }

            if (!DisplayShape.TryParse(options.DisplayShapeString, out var shape))
            {
                Console.WriteLine($"Error: '{options.DisplayShapeString}' is not a valid display shape. Expected format: WxH (e.g. 1x1, 2x1, 4x2).");

                return false;
            }

            if (!options.OutputToFile && !options.PasteToClipBoard && !options.PrintOutput)
            {
                if (PromptYesNo("No output specified. Save to file?"))
                    options.OutputFile = Path.ChangeExtension(options.InputFile, ".schem");
                else
                    return false;
            }




            if (options.ImageDitherMethods.Count() == 0)
                options.ImageDitherMethods = [DitherMethod.No];
            if (options.ImageResolutions.Count() == 0)
                options.ImageResolutions = [176];
            if (options.ImageColorAmounts.Count() == 0)
                options.ImageColorAmounts = [16];

            return true;
        }

#pragma warning disable CS8618
        public static Options options;
        public static MagickImage FullImage;
#pragma warning restore CS8618
        static void Run()
        {
            MagickImage image = new MagickImage(File.ReadAllBytes(options.InputFile));
            image.Flip();
            FullImage = image;

            if (options.Debug)
            {
                string debugImageOutPath = Path.GetTempPath() + Guid.NewGuid().ToString() + ".jpg";
                image.Write(new FileInfo(debugImageOutPath));
                Console.WriteLine("Wrote proccessed image into: " + debugImageOutPath);
            }

            Schematic schem = new();
            schem.Name = options.SchematicName;

            DisplayCluster.BuildCluster(FullImage, options.DisplayShape.Width, options.DisplayShape.Height).FillSchem(schem);

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

            ConfirmOutput(options, schem);
        }
        static void ConfirmOutput(Options options, Schematic schem)
        {
            StringBuilder output = new();

            output.AppendLine($"Successfully created schematic: {options.SchematicName}");
            output.AppendLine($"Input: {options.InputFile}");
            output.AppendLine($"Output resolution(s): {string.Join(", ", options.ImageResolutions)}x{string.Join(", ", options.ImageResolutions)}");
            output.AppendLine($"Color amount(s): {string.Join(", ", options.ImageColorAmounts)}");
            output.AppendLine($"Dittering Method: {string.Join(", ", options.ImageDitherMethods)}");
            output.AppendLine($"Proccessors used: {schem.Blocks.Where(s => s.data.Name.Contains("processor")).ToArray().Length}");
            output.AppendLine($"Total blocks: {schem.Blocks.Count}");
            if (options.OutputToFile)
                output.AppendLine($"Output: {options.OutputFile}");
            if (options.PasteToClipBoard)
                output.AppendLine($"Successfully pasted to clipboard.");
            if (options.Verbose)
            {
                // TODO: make this more accurate, rn it quantizes the whole image instead of all the sections which isnt accurate as not each section will have the same color palette
                output.AppendLine("v:");
                IMagickImage<byte> colorCountImage = FullImage.Clone();
                colorCountImage.Resize(new MagickGeometry(options.ImageResolutions.ToArray()[0], options.ImageResolutions.ToArray()[0]) { IgnoreAspectRatio = true });
                colorCountImage.Quantize(new QuantizeSettings { Colors = options.ImageColorAmounts.ToArray()[0], DitherMethod = options.ImageDitherMethods.ToArray()[0] });
                IReadOnlyDictionary<IMagickColor<byte>, uint> histogramResult = colorCountImage.Histogram();

                List<Color> usedColors = new List<Color>();
                foreach (KeyValuePair<IMagickColor<byte>, uint> histo in histogramResult)
                { // idk what im suppose to name the item tbh
                    usedColors.Add(new(histo.Key.R, histo.Key.G, histo.Key.B, histo.Key.A));
                }
                //output.AppendLine("\t" + "Color count: " + usedColors.Count);
                output.AppendLine("\t" + "Colors:" + usedColors.Count);

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
