# IMG2SCHEM — Image to Mindustry Schematic



requires https://github.com/DeCEll-1/MindustryChematicCreator as dependency (included in the bin file)

4x6 from: https://x.com/Green_Kohgen/status/1003582195728932870

```
  -i, --input               Required. Input image to be processed.

  -o, --output              Output path to place the schematic base64.

  -r, --image-resolution    (Default: 176) The resolution(s) to resize the input image to for a
                            display. 176 is the max resolution, 88 will render half resolution etc.
                            etc.
                            Multiple space-separated values (e.g. 44 88 176) will cycle through the
                            list for each display tile (from left to right top to bottom)

  -c, --color-amount        (Default: 16) Number of distinct colors used for displays. Use a single
                            value (e.g. 96) for consistent quality across the entire image.
                            Multiple space-separated values (e.g. 8 16 32) will cycle through the
                            list for each display tile (from left to right top to bottom)
                            /!\ Not suggested to input more than 48 as it will increase the amount
                            of processors significantly.
                            [!] This is a good setting to reduce if theres too many processor usage.

  -d, --dithering-method    (Default: No) Dithering algorithm(s) to reduce color banding. Available:
                            No, Riemersma, FloydSteinberg.
                            Multiple space-separated values (e.g. No Riemersma FloydSteinberg) will
                            cycle through the list for each display tile (from left to right top to
                            bottom)

  -v, --verbose             Detailed outputs.

  --debug                   Debug mode.

  -y, --yes                 Override output files.

  -n, --no                  Never override output files.

  -p, --clipboard           (Default: false) Pastes the output schematic to clipboard.

  --print-output            Print output schematic Base64 to console.

  --name                    (Default: Unnamed) Name of the output schematic.

  -s, --display-shape       (Default: 1x1) The shape of the resulting display (e.g. 1x1, 2x1, 4x2)

  --help                    Display this help screen.

  --version                 Display version information
```

Examples:

1. Save output to file instead of clipboard
IMG2SCHEM -i "image.png" -o "image.msch"

2. Use multiple resolutions cycling per tile
IMG2SCHEM -i "image.jpg" -s 2x2 -r 44 88 176

3. Force overwrite + custom name
IMG2SCHEM -i "image.png" -o "image.msch" -y --name "MyDisplay"

4. High color amount
IMG2SCHEM -i "image.png" -c 48

5. Cycle dithering methods
IMG2SCHEM -i "image.png" -d No Riemersma FloydSteinberg

6. resolution + color + dithering
IMG2SCHEM -i "image.png" -s 3x3 -r 88 176 -c 32 48 -d Riemersma

7. auto-yes, output to file
IMG2SCHEM -i "image.png" -o "map_disp.msch" -y --print-output

8. Debug mode
IMG2SCHEM -i "image.png" --debug -v

9. High quality 4×6 display grid
IMG2SCHEM -i "image.png" -s 4x6 -r 176 -c 96

10. Minimal
IMG2SCHEM -i "image.png" -c 8 -d No
