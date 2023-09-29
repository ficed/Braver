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

    public class BattleExport : ExportKind {
        private BattleModelOptions _options = new BattleModelOptions();

        public override string Help =>
@"Usage: CrossSlash BattleExport [SourceLGPOrFolder] [OutputFile] [ModelCode] [options]
        ModelCode can be a two letter battle model code (e.g. RV), or a summon model code (e.g. cyvadat)
    Valid options:
        /ConvertSRGBToLinear - convert colours to linear RGB
        /SwapWinding - reverse triangle winding order
        /BakeVertexColours - export vertex colours as a texture
        /Scale:value - scale model up/down from default size
    For example:
        CrossSlash BattleModel C:\games\FF7\data\battle\magic.lgp C:\temp\shiva.glb cyvadat /Scale:0.1
";

        public override object Config => _options;
        public override bool HasGui => true;
        public override string Name => "Battle Model Export";

        public override void Execute(DataSource source, string dest, IEnumerable<string> parameters) {
            var exporter = new BattleModel(source, _options);
            var model = exporter.BuildSceneAuto(parameters.First());
            model.SaveGLB(dest);
        }

        public override Window ExecuteGui(DataSource source) => new BattleExportWindow(source);
    }

    public class BattleExportWindow : Window {
        private Label _lblGLB;
        private string _glbFile;
        private DataSource _source;
        private CheckBox _chkSRGB, _chkSwapWinding, _chkBakeColours;
        private TextField _txtModel, _txtScale;

        public BattleExportWindow(DataSource source) {
            _source = source;
            Title = "CrossSlash Exporter (Ctrl-Q to Quit)";

            Label lblModel = new Label {
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

            Add(lblModel, _txtModel, lblScale, _txtScale, 
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

        private void BtnExport_Clicked() {
            try {
                if (_source == null)
                    throw new Exception("No data source selected");
                if (string.IsNullOrEmpty(_glbFile))
                    throw new Exception("No GLB save as filename selected");
                if (string.IsNullOrWhiteSpace(_txtModel.Text.ToString()))
                    throw new Exception("No model file selected");
                if (!float.TryParse(_txtScale.Text.ToString(), out float scale))
                    throw new Exception("No scale specified");

                var options = new BattleModelOptions {
                    ConvertSRGBToLinear = _chkSRGB.Checked,
                    SwapWinding = _chkSwapWinding.Checked,
                    BakeVertexColours = _chkBakeColours.Checked,
                    Scale = scale,
                };
                var exporter = new BattleModel(_source, options);
                var model = exporter.BuildSceneAuto(_txtModel.Text.ToString());
                model.SaveGLB(_glbFile);

                MessageBox.Query("Success", "Export Succeeded", "OK");
            } catch (Exception ex) {
                MessageBox.ErrorQuery("Error", ex.Message, "OK");
            }
        }
    }
}
