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
    public class BattleExportGuiWindow : Window {
        private Label _lblLGP, _lblGLB;
        private string _lgpFile, _glbFile;
        private CheckBox _chkSRGB, _chkSwapWinding, _chkBakeColours;
        private TextField _txtModel, _txtScale;

        public BattleExportGuiWindow() {
            Title = "CrossSlash Exporter (Ctrl-Q to Quit)";

            var btnLGP = new Button {
                Text = "Select battle LGP file",
                Width = Dim.Percent(25),
            };
            btnLGP.Clicked += BtnLGP_Clicked;

            _lblLGP = new Label {
                Text = "(No LGP selected)",
                X = Pos.Right(btnLGP) + 1,
            };

            Label lblModel = new Label {
                Y = Pos.Bottom(btnLGP) + 1,
                Width = Dim.Percent(25),
                Text = "Model Code",
            };
            _txtModel = new TextField {
                Y = lblModel.Y,
                X = Pos.Right(lblModel),
                Width = Dim.Fill(1),
            };

            Label lblScale = new Label {
                Y = Pos.Bottom(lblModel) + 1,
                Width = Dim.Percent(25),
                Text = "Scale",
            };
            _txtScale = new TextField {
                Y = lblScale.Y,
                X = Pos.Right(lblScale),
                Width = Dim.Fill(1),
                Text = "1",
            };

            _chkSRGB = new CheckBox {
                Checked = true,
                Text = "Convert colours from SRGB->Linear",
                Y = Pos.Bottom(_txtScale) + 1,
            };
            _chkSwapWinding = new CheckBox {
                Checked = false,
                Text = "Swap triangle winding",
                Y = Pos.Bottom(_chkSRGB),
            };
            _chkBakeColours = new CheckBox {
                Checked = false,
                Text = "Bake Vertex Colours to texture",
                Y = Pos.Bottom(_chkSwapWinding),
            };

            var btnGLB = new Button {
                Text = "Save GLB As",
                Width = Dim.Percent(25),
                Y = Pos.Bottom(_chkBakeColours) + 1,
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

            Add(btnLGP, _lblLGP, lblModel, _txtModel, lblScale, _txtScale, 
                _chkSRGB, _chkSwapWinding, _chkBakeColours,
                btnGLB, _lblGLB, btnExport);
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
                "Open LGP", "Choose the battle.lgp file to read models from",
                new List<string> { ".lgp" }
            );
            Application.Run(d);
            if (!d.Canceled && d.FilePaths.Any()) {
                _lblLGP.Text = _lgpFile = d.FilePaths[0];
            }
        }

        private void BtnExport_Clicked() {
            try {
                if (!File.Exists(_lgpFile))
                    throw new Exception("No LGP file selected");
                if (string.IsNullOrEmpty(_glbFile))
                    throw new Exception("No GLB save as filename selected");
                if (string.IsNullOrWhiteSpace(_txtModel.Text.ToString()))
                    throw new Exception("No model file selected");
                if (!float.TryParse(_txtScale.Text.ToString(), out float scale))
                    throw new Exception("No scale specified");

                using (var lgp = new Ficedula.FF7.LGPFile(_lgpFile)) {
                    var exporter = new Ficedula.FF7.Exporters.BattleModel(lgp) {
                        ConvertSRGBToLinear = _chkSRGB.Checked,
                        SwapWinding = _chkSwapWinding.Checked,
                        Scale = scale,
                    };
                    var model = exporter.BuildSceneAuto(_txtModel.Text.ToString());
                    model.SaveGLB(_glbFile);
                }
                MessageBox.Query("Success", "Export Succeeded", "OK");
            } catch (Exception ex) {
                MessageBox.ErrorQuery("Error", ex.Message, "OK");
            }
        }
    }
}
