// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using CrossSlash;
using Ficedula;
using Ficedula.FF7.Exporters;
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
        if (ExportKind.Exporters.TryGetValue(args[0], out var exporter)) {
            Console.WriteLine(exporter.Help);
        } else {
            Console.WriteLine("USAGE: CrossSlash [ExportType] [SourceLGPOrFolder] [OutputFile] [parameters...]");
            Console.WriteLine("ExportTypes: " + string.Join(", ", ExportKind.Exporters.Keys));
            Console.WriteLine("Use CrossSlash [ExportType] for help on a specific exporter");
        }
        break;

    default:
        exporter = ExportKind.Exporters[args[0]];
        Console.WriteLine($"Opening source {args[1]}...");
        var source = DataSource.Create(args[1]);
        string dest = args[2];
        var options = args.Skip(2)
            .Where(s => s.StartsWith('/'))
            .Select(s => s.Substring(1).Split(':'))
            .ToDictionary(
                sa => sa[0],
                sa => sa.ElementAtOrDefault(1) ?? "true",
                StringComparer.InvariantCultureIgnoreCase
            );
        var parameters = args.Skip(3).Where(s => !s.StartsWith('/'));
        Serialisation.SetProperties(exporter.Config, options);
        exporter.Execute(source, dest, parameters);
        Console.WriteLine("Done");

        break;
}
