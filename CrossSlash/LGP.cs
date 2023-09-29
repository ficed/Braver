// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7.Exporters;
using System;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using Terminal.Gui;

namespace CrossSlash {

    public class LGPExportOptions {
        public bool List { get; set; }
    }

    public class LGP : ExportKind {
        public override string Help =>
@"Usage: CrossSlash LGP [SourceLGP] [DestFolder] [/List] or [file] [file] [file...]
    Extracts files from an LGP, or lists LGP contents
    Use /List to list the contents of the LGP;
    or list the files to extract from the LGP.
    Files can be an exact filename or use *? wildcards, e.g.

    CrossSlash LGP flevel.lgp C:\temp mrkt* nmk*
    
";

        private LGPExportOptions _options = new();
        public override object Config => _options;

        public override bool HasGui => true;

        public override string Name => "LGP List/Extract";

        public override void Execute(DataSource source, string dest, IEnumerable<string> parameters) {
            if (_options.List) {
                Console.WriteLine("Size      File");
                Console.WriteLine("==============");
                foreach (string filename in source.AllFiles) {
                    using(var s = source.Open(filename)) {
                        Console.WriteLine($"{s.Length,9} {filename}");
                    }
                }
            } else {
                foreach(string file in source.AllFiles) {
                    if (parameters.Any(f => FileSystemName.MatchesSimpleExpression(f, file))) {
                        Console.WriteLine($"Extracting {file}...");
                        using(var s = source.Open(file)) {
                            string output = Path.Combine(dest, file);
                            Directory.CreateDirectory(Path.GetDirectoryName(output));
                            using (var fs = File.OpenWrite(output)) {
                                s.CopyTo(fs);
                            }
                        }
                    }
                }
            }
        }

        public override Window ExecuteGui(DataSource source) => new LGPWindow(source);
    }

    public class LGPWindow : Window {
        private DataSource _source;
        private ListView _lvFiles;
        private List<string> _entries;
        private string _dest;

        public LGPWindow(DataSource source) { 
            _source = source;
            Title = "LGP List/Extract";

            _lvFiles = new ListView {
                Width = Dim.Fill(1),
                Height = 15,
                AllowsMarking = true,
            };
            _entries = source.AllFiles.ToList();
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

            foreach(int i in Enumerable.Range(0, _entries.Count)) {
                if (_lvFiles.Source.IsMarked(i)) {
                    using(var s = _source.Open(_entries[i])) {
                        using (var fs = File.OpenWrite(Path.Combine(_dest, _entries[i]))) {
                            s.CopyTo(fs);
                        }
                    }
                }
            }

            MessageBox.Query("Done", "Export complete", "OK");
        }
    }
}
