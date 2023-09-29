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
using System.Threading.Tasks;
using Terminal.Gui;

namespace CrossSlash {
    public class Audio : ExportKind {
        public override string Help =>
@"
    Usage: CrossSlash Audio [SoundFolder] [DestFolder] [AudioID] [AudioID] [AudioID...]
        SoundFolder should be the folder containing audio.fmt/audio.dat
        AudioID can either be the numeric ID of a sound effect, or * to extract all.
        e.g.
        CrossSlash Audio C:\FF7\data\sound C:\temp\audio 0 1 33 35

        Sounds are exported to files named <soundID>.wav in the specified destination folder.
";

        public override object Config => null;

        public override bool HasGui => true;

        public override string Name => "Audio Extractor";

        public override void Execute(DataSource source, string dest, IEnumerable<string> parameters) {
            var audio = new Ficedula.FF7.Audio(
                source.Open("audio.dat"),
                source.Open("audio.fmt")
            );
            foreach(string parm in parameters) {
                IEnumerable<int> range;
                if (parm == "*")
                    range = Enumerable.Range(0, audio.EntryCount);
                else
                    range = Enumerable.Repeat(int.Parse(parm), 1);

                foreach(int id in range) {
                    if (audio.IsValid(id)) {
                        Console.WriteLine($"Exporting sound {id}");
                        using (var fs = File.OpenWrite(Path.Combine(dest, $"{id}.wav")))
                            audio.Export(id, fs);
                    } else
                        Console.WriteLine($"Skipping invalid sound effect {id}");

                }
            }
        }

        public override Window ExecuteGui(DataSource source) => new AudioWindow(source, this);
    }

    public class AudioWindow : Window {
        private DataSource _source;
        private Audio _exporter;
        private string? _dest;
        private TextView _txtSounds;

        public AudioWindow(DataSource source, Audio exporter) { 
            _source = source;
            _exporter = exporter;

            Label lblSounds = new Label {
                Text = "Sound IDs to export (leave blank for all)",
            };
            _txtSounds = new TextView {
                Y = Pos.Bottom(lblSounds),
                Width = Dim.Fill(1),
                Height = Dim.Sized(10),
            };

            Button btnDest = new Button {
                Y = Pos.Bottom(_txtSounds) + 2,
                Text = "(No destination folder set)",
                Width = Dim.Fill(5),
            };
            btnDest.Clicked += () => {
                var d = new OpenDialog(
                    "Open Folder", "Choose the folder to extract to",
                    openMode: OpenDialog.OpenMode.Directory
                );
                Application.Run(d);
                if (!d.Canceled && d.FilePaths.Any()) {
                    _dest = d.FilePaths[0];
                    btnDest.Text = "Extract to " + _dest;
                }
            };

            var btnExport = new Button {
                Y = Pos.Bottom(btnDest) + 1,
                Width = Dim.Fill(1),
                Text = "Export"
            };
            btnExport.Clicked += BtnExport_Clicked;

            Add(lblSounds, _txtSounds, btnDest, btnExport);

        }

        private void BtnExport_Clicked() {
            if (string.IsNullOrEmpty(_dest)) {
                MessageBox.ErrorQuery("Error", "Select a destination folder", "OK");
                return;
            }

            try {
                var ids = _txtSounds.Text
                    .ToString()
                    .Split('\r', '\n')
                    .Where(s => !string.IsNullOrWhiteSpace(s));

                if (ids.Any())
                    _exporter.Execute(_source, _dest, ids);
                else
                    _exporter.Execute(_source, _dest, new[] { "*" });

                MessageBox.Query("Done", "Export complete", "OK");
            } catch (Exception ex) {
                MessageBox.ErrorQuery("Error", ex.Message, "OK");
            }
        }
    }
}
