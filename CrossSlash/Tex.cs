// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7.Exporters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terminal.Gui;

namespace CrossSlash {
    public class Tex : ExportKind {
        public override string Help =>
@"
    Usage: CrossSlash Tex [SourceLGP] [DestinationFolder] [file:palette] [file:palette] ...
        For each file, specify the palette number to export, or * for all palettes.
        Exported textures are written to PNG files.
        Example:
        CrossSlash Tex C:\FF7\data\battle\magic.lgp C:\temp\texs bio.tex:0 bio.tex:1
        CrossSlash Tex C:\FF7\data\battle\magic.lgp C:\temp\texs fire00.tex:*
";

        public override object Config => null;

        public override bool HasGui => true;

        public override string Name => "Tex export";

        public override void Execute(DataSource source, string dest, IEnumerable<string> parameters) {
            foreach(string tex in parameters) {
                string[] parts = tex.Split(':');
                using(var s = source.Open(parts[0])) {
                    var texFile = new Ficedula.FF7.TexFile(s);
                    IEnumerable<int> palettes;
                    if (parts[1] == "*")
                        palettes = Enumerable.Range(0, texFile.Palettes.Count);
                    else
                        palettes = Enumerable.Range(int.Parse(parts[1]), 1);
                    foreach(int pal in palettes) {
                        string output = Path.Combine(dest, Path.ChangeExtension(parts[0], $"#{pal}.png"));
                        Console.WriteLine("Writing " + output);
                        Directory.CreateDirectory(Path.GetDirectoryName(output));
                        using (var bmp = TexFileUtil.ToBitmap(texFile, pal)) {
                            File.WriteAllBytes(
                                output,
                                bmp.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100).ToArray()
                            );
                        }
                    }
                }
            }
        }

        public override Window ExecuteGui(DataSource source) => new TexWindow(source, this);
    }

    public class TexWindow : Window {
        private DataSource _source;
        private Tex _exporter;
        private string _dest;
        private ListView _lvFiles;
        private List<string> _entries;

        public TexWindow(DataSource source, Tex exporter) {
            _source = source;
            _exporter = exporter;
            Title = "Tex bitmap extractor";

            _lvFiles = new ListView {
                Width = Dim.Fill(1),
                Height = 15,
                AllowsMarking = true,
            };
            _entries = source.AllFiles
                .Where(s => Path.GetExtension(s).Equals(".tex", StringComparison.InvariantCultureIgnoreCase))
                .ToList();
            _lvFiles.SetSource(_entries);

            Button bDest = new Button {
                Y = Pos.Bottom(_lvFiles) + 2,
                Text = "(No destination folder set)",
                Width = Dim.Fill(5),
            };
            bDest.Clicked += () => {
                var d = new OpenDialog(
                    "Open Folder", "Choose the folder to extract to",
                    openMode: OpenDialog.OpenMode.Directory
                );
                Application.Run(d);
                if (!d.Canceled && d.FilePaths.Any()) {
                    _dest = d.FilePaths[0];
                    bDest.Text = "Extract to " + _dest;
                }
            };

            Button bExtract = new Button {
                Y = Pos.Bottom(bDest) + 2,
                Text = "Extract Selected Files",
                Width = Dim.Fill(5),
            };
            bExtract.Clicked += BExtract_Clicked;

            Add(_lvFiles, bDest, bExtract);

        }

        private void BExtract_Clicked() {
            if (string.IsNullOrEmpty(_dest)) {
                MessageBox.ErrorQuery("Error", "No destination folder selected", "OK");
                return;
            }

            var files = Enumerable.Range(0, _entries.Count)
                .Where(i => _lvFiles.Source.IsMarked(i))
                .Select(i => _entries[i] + ":*");

            _exporter.Execute(_source, _dest, files);

            MessageBox.Query("Done", "Export complete", "OK");

        }
    }
}
