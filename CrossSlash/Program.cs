// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using CrossSlash;
using Terminal.Gui;

System.Globalization.CultureInfo.CurrentCulture =
    System.Globalization.CultureInfo.CurrentUICulture =
    System.Globalization.CultureInfo.DefaultThreadCurrentCulture =
    System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

Console.WriteLine("CrossSlash"); 

switch (args.Length) {
    case 0:
        Application.Run<SplashWindow>();
        Application.Shutdown();
        break;  
    case 1:
    case 2:
    case 3:
        Console.WriteLine("USAGE: CrossSlash [OutputGLBFile] [LGPFile] [HRCFile] {/ConvertSRGB} {/SwapWinding} [Anim] [Anim] [Anim]...");
        Console.WriteLine(@"e.g. CrossSlash C:\temp\tifa.glb C:\games\FF7\data\field\char.lgp AAGB.HRC ABCD.a ABCE.a");
        Console.WriteLine(@"or:");
        Console.WriteLine(@"CrossSlash [OutputGLBFile] [LGPFile] [BattleModelCode] {/ConvertSRGB} {/SwapWinding} {/Scale:1}");
        Console.WriteLine("");
        Console.WriteLine("Specify /ConvertSRGB to convert vertex colours from SRGB to linear when exporting");
        Console.WriteLine("Specify /SwapWinding to swap triangle winding order");
        break;
    default:
        Console.WriteLine($"Opening LGP {args[1]}...");
        using (var lgp = new Ficedula.FF7.LGPFile(args[1])) {

            void Configure(Ficedula.FF7.Exporters.ModelBase exporter) {
                exporter.ConvertSRGBToLinear = args.Any(s => s.Equals("/ConvertSRGB", StringComparison.InvariantCultureIgnoreCase));
                exporter.SwapWinding = args.Any(s => s.Equals("/SwapWinding", StringComparison.InvariantCultureIgnoreCase));
            }

            if (args[2].EndsWith(".HRC", StringComparison.InvariantCultureIgnoreCase)) {
                var exporter = new Ficedula.FF7.Exporters.FieldModel(lgp);
                Configure(exporter);

                Console.WriteLine($"Exporting model {args[2]}...");
                var model = exporter.BuildScene(args[2], args.Skip(3).Where(s => !s.StartsWith("/")));
                Console.WriteLine($"Saving output to {args[0]}...");
                model.SaveGLB(args[0]);
            } else if (args[2].Length == 2) {
                var exporter = new Ficedula.FF7.Exporters.BattleModel(lgp);
                Configure(exporter);
                exporter.Scale = float.Parse(
                    args
                    .Where(s => s.StartsWith("/Scale:", StringComparison.InvariantCultureIgnoreCase))
                    .Select(s => s.Substring("/Scale:".Length))
                    .FirstOrDefault()
                    ?? exporter.Scale.ToString()
                );
                Console.WriteLine($"Exporting model {args[2]}...");
                var bmodel = exporter.BuildSceneFromModel(args[2]);
                Console.WriteLine($"Saving output to {args[0]}...");
                bmodel.SaveGLB(args[0]);
            } else
                throw new Exception("Unrecognised export type");

            Console.WriteLine("Done");
        }
        break;
}
