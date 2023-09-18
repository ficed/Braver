// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace CrossSlash {
    public class FieldExportGuiWindow : Window {

        private Label _lblLGP, _lblGLB;
        private ListView _lvHRCs;
        private TextView _txtAnims;
        private List<string> _hrcFiles;
        private CheckBox _chkSRGB, _chkSwapWinding;

        private string _lgpFile, _glbFile;

        public FieldExportGuiWindow() {
            Title = "CrossSlash Exporter (Ctrl-Q to Quit)";

            var btnLGP = new Button {
                Text = "Select char LGP file",
                Width = Dim.Percent(25),
            };
            btnLGP.Clicked += BtnLGP_Clicked;

            _lblLGP = new Label {
                Text = "(No LGP selected)",
                X = Pos.Right(btnLGP) + 1,
            };

            Label lblHRC = new Label {
                Y = Pos.Bottom(btnLGP) + 1,
                Width = Dim.Percent(25),
                Text = "HRC/Model",
            };

            _lvHRCs = new ListView {
                Y = lblHRC.Y,
                X = Pos.Right(lblHRC),
                Width = Dim.Fill(1),
                Height = 8,
            };

            Label lblAnims = new Label {
                Width = Dim.Percent(25),
                Y = Pos.Bottom(_lvHRCs) + 1,
                Text = "Animations:"
            };

            _txtAnims = new TextView {
                X = Pos.Right(lblAnims) + 1,
                Y = lblAnims.Y,
                Width = Dim.Fill(),
                Height = 8,
            };

            _chkSRGB = new CheckBox {
                Checked = true,
                Text = "Convert colours from SRGB->Linear",
                Y = Pos.Bottom(_txtAnims) + 1,
            };
            _chkSwapWinding = new CheckBox {
                Checked = false,
                Text = "Swap triangle winding",
                Y = Pos.Bottom(_chkSRGB) + 1,
            };

            var btnGLB = new Button {
                Text = "Save GLB As",
                Width = Dim.Percent(25),
                Y = Pos.Bottom(_chkSwapWinding) + 1,
            };
            btnGLB.Clicked += BtnGLB_Clicked;

            _lblGLB = new Label {
                X = Pos.Right(btnGLB) + 1,
                Y = btnGLB.Y,
                Text = "(No file selected)",
            };

            var btnExport = new Button {
                Y = Pos.Bottom(btnGLB) + 1,
                Width = Dim.Fill(1),
                Text = "Export"
            };
            btnExport.Clicked += BtnExport_Clicked;

            Add(btnLGP, _lblLGP, lblHRC, _lvHRCs, lblAnims, _txtAnims, _chkSRGB, _chkSwapWinding, btnGLB, _lblGLB, btnExport);
        }

        private void BtnExport_Clicked() {
            try {
                var anims = _txtAnims.Text
                    .ToString()
                    .Split('\r', '\n')
                    .Where(s => !string.IsNullOrWhiteSpace(s));

                if (!File.Exists(_lgpFile))
                    throw new Exception("No LGP file selected");
                if (string.IsNullOrEmpty(_glbFile))
                    throw new Exception("No GLB save as filename selected");
                if (_lvHRCs.SelectedItem < 0)
                    throw new Exception("No HRC file selected");
                if (!anims.Any())
                    throw new Exception("No animations specified");

                using (var lgp = new Ficedula.FF7.LGPFile(_lgpFile)) {
                    var exporter = new Ficedula.FF7.Exporters.FieldModel(lgp) {
                        ConvertSRGBToLinear = _chkSRGB.Checked,
                        SwapWinding = _chkSwapWinding.Checked,
                    };
                    var model = exporter.BuildScene(_hrcFiles[_lvHRCs.SelectedItem], anims);
                    model.SaveGLB(_glbFile);
                }
                MessageBox.Query("Success", "Export Succeeded", "OK");
            } catch (Exception ex) {
                MessageBox.ErrorQuery("Error", ex.Message, "OK");
            }
        }

        private void BtnGLB_Clicked() {
            var d = new SaveDialog(
                "Save As", "Save output model to which file",
                new List<string> { ".glb" }
            );
            Application.Run(d);
            if (!d.Canceled && d.FileName != null) {
                _lblGLB.Text = _glbFile = (string)d.FilePath;
            }
        }

        private void BtnLGP_Clicked() {
            var d = new OpenDialog(
                "Open LGP", "Choose the char.lgp file to read models from",
                new List<string> { ".lgp" }
            );
            Application.Run(d);
            if (!d.Canceled && d.FilePaths.Any()) {
                try {
                    using (var lgp = new Ficedula.FF7.LGPFile(d.FilePaths[0])) {
                        _hrcFiles = lgp.Filenames
                            .Where(s => Path.GetExtension(s).Equals(".hrc", StringComparison.InvariantCultureIgnoreCase))
                            .ToList();
                        _lvHRCs.SetSource(_hrcFiles);
                    }
                } catch (Exception ex) {
                    MessageBox.ErrorQuery("Error", ex.Message, "OK");
                }
                _lblLGP.Text = _lgpFile = d.FilePaths[0];
            }
        }
    }
}
