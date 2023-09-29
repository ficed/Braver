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
    public class SplashWindow : Window {

        private Label _lblLGP;
        private DataSource _source;

        public SplashWindow() {

            Title = "CrossSlash FF7 exporter";

            var btnLGP = new Button {
                Text = "Select LGP file",
                Width = Dim.Percent(25),
            };
            btnLGP.Clicked += () => DoOpen(true);

            var btnFolder = new Button {
                Text = "Select folder",
                Width = Dim.Percent(25),
                X = Pos.Right(btnLGP) + 1,
            };
            btnFolder.Clicked += () => DoOpen(false);

            _lblLGP = new Label {
                Text = "(No source selected)",
                X = Pos.Right(btnFolder) + 1,
            };

            Add(btnLGP, btnFolder, _lblLGP);

            Pos y = Pos.Bottom(_lblLGP) + 2;

            foreach(var exporter in ExportKind.Exporters.Values.Where(e => e.HasGui)) {
                Button b = new Button {
                    Text = exporter.Name,
                    Width = Dim.Fill(2),
                    Y = y,
                };
                b.Clicked += () => {
                    if (_source == null) {
                        MessageBox.ErrorQuery("Error", "Select a source LGP or folder", "OK");
                        return;
                    }
                    Application.RequestStop();
                    Application.Run(exporter.ExecuteGui(_source));
                };
                y = Pos.Bottom(b) + 1;
                Add(b);
            }

        }

        private void DoOpen(bool lgp) {
            OpenDialog d;
            if (lgp)
                d = new OpenDialog(
                    "Open LGP", "Choose the LGP file to read data from",
                    new List<string> { ".lgp" }
                );
            else
                d = new OpenDialog(
                    "Open Folder", "Choose the folder to read data from",
                    openMode: OpenDialog.OpenMode.Directory
                );
            Application.Run(d);
            if (!d.Canceled && d.FilePaths.Any()) {
                try {
                    _source = DataSource.Create(d.FilePaths[0]);
                } catch (Exception ex) {
                    MessageBox.ErrorQuery("Error", ex.Message, "OK");
                }
                _lblLGP.Text = d.FilePaths[0];
            }
        }

    }
}
