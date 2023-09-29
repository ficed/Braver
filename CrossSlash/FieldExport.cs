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

    public class FieldExport : ExportKind {
        private ModelBaseOptions _config = new ModelBaseOptions();
        public override string Help =>
@"Usage: CrossSlash FieldExport [SourceLGPOrFolder] [OutputFile] [HRCFile] [anim1] [anim2] [anim3...] [options]
    Valid options:
        /ConvertSRGBToLinear - convert colours to linear RGB
        /SwapWinding - reverse triangle winding order
        /BakeVertexColours - export vertex colours as a texture
    For example:
        CrossSlash FieldModel C:\games\FF7\data\field\char.lgp C:\temp\cloud.glb AAAA.HRC ACFE.a AAFF.a /SwapWinding
";

        public override object Config => _config;
        public override bool HasGui => true;
        public override string Name => "Field Model Export";

        public override void Execute(DataSource source, string dest, IEnumerable<string> parameters) {
            var exporter = new FieldModel(source, _config);
            Console.WriteLine($"Exporting model {parameters.First()}...");
            var model = exporter.BuildScene(parameters.First(), parameters.Skip(1));
            Console.WriteLine($"Saving output to {dest}...");
            model.SaveGLB(dest);
        }

        public override Window ExecuteGui(DataSource source) => new FieldExportWindow(source);
    }

    public class FieldExportWindow : Window {

        private Label _lblGLB;
        private ListView _lvHRCs;
        private TextView _txtAnims;
        private List<string> _hrcFiles;
        private CheckBox _chkSRGB, _chkSwapWinding, _chkBakeColours;

        private string _glbFile;
        private DataSource _source;

        public FieldExportWindow(DataSource source) {
            _source = source;
            Title = "CrossSlash Exporter (Ctrl-Q to Quit)";

            Label lblHRC = new Label {
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

            _hrcFiles = _source.AllFiles
                .Where(s => Path.GetExtension(s).Equals(".hrc", StringComparison.InvariantCultureIgnoreCase))
                .ToList();
            _lvHRCs.SetSource(_hrcFiles);


            Add(lblHRC, _lvHRCs, lblAnims, _txtAnims, 
                _chkSRGB, _chkSwapWinding, _chkBakeColours,
                btnGLB, _lblGLB, btnExport);
        }

        private void BtnExport_Clicked() {
            try {
                var anims = _txtAnims.Text
                    .ToString()
                    .Split('\r', '\n')
                    .Where(s => !string.IsNullOrWhiteSpace(s));

                if (_source == null)
                    throw new Exception("No data source selected");
                if (string.IsNullOrEmpty(_glbFile))
                    throw new Exception("No GLB save as filename selected");
                if (_lvHRCs.SelectedItem < 0)
                    throw new Exception("No HRC file selected");
                if (!anims.Any())
                    throw new Exception("No animations specified");

                var options = new ModelBaseOptions {
                    ConvertSRGBToLinear = _chkSRGB.Checked,
                    SwapWinding = _chkSwapWinding.Checked,
                    BakeVertexColours = _chkBakeColours.Checked,
                };
                var exporter = new FieldModel(_source, options);
                var model = exporter.BuildScene(_hrcFiles[_lvHRCs.SelectedItem], anims);
                model.SaveGLB(_glbFile);

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

    }
}
