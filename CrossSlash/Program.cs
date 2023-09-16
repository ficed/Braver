// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using CrossSlash;
using Terminal.Gui;

Console.WriteLine("CrossSlash");

switch (args.Length) {
    case 0:
        Application.Run<ExportGuiWindow>();
        Application.Shutdown();
        break;  
    case 1:
    case 2:
    case 3:
        Console.WriteLine("USAGE: CrossSlash [OutputGLBFile] [LGPFile] [HRCFile] [Anim] [Anim] [Anim]...");
        Console.WriteLine(@"e.g. CrossSlash C:\temp\tifa.glb C:\games\FF7\data\field\char.lgp AAGB.HRC ABCD.a ABCE.a");
        break;
    default:
        Console.WriteLine($"Opening LGP {args[1]}...");
        using (var lgp = new Ficedula.FF7.LGPFile(args[1])) {
            var exporter = new Ficedula.FF7.Exporters.FieldModel(lgp);
            Console.WriteLine($"Exporting model {args[2]}...");
            var model = exporter.BuildScene(args[2], args.Skip(3));
            Console.WriteLine($"Saving output to {args[0]}...");
            model.SaveGLB(args[0]);
            Console.WriteLine("Done");
        }
        break;
}
