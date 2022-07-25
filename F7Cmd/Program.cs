// See https://aka.ms/new-console-template for more information
using Ficedula.FF7.Exporters;

Console.WriteLine("F7Cmd");

if (args.Length < 2) return;

if (args[0].Equals("LGP", StringComparison.OrdinalIgnoreCase)) {
    using(var lgp = new Ficedula.FF7.LGPFile(args[1])) {
        Console.WriteLine($"LGP file {args[1]}");
        foreach(string file in lgp.Filenames) {
            using(var data = lgp.Open(file)) {
                Console.WriteLine($"  {file} size {data.Length}");
            }
        }
    }
}

if (args[0].Equals("Sounds", StringComparison.OrdinalIgnoreCase)) {
    using (var audio = new Ficedula.FF7.Audio(Path.Combine(args[1], "audio.dat"), Path.Combine(args[1], "audio.fmt"))) {
        Console.WriteLine($"Audio file with {audio.EntryCount} entries");
        var ms = new MemoryStream();
        audio.Export(10, ms);
        File.WriteAllBytes(@"C:\temp\tff.wav", ms.ToArray());
        File.WriteAllBytes(@"C:\temp\tff.raw", audio.ExportPCM(10, out int freq, out int chans));
    }
}

if (args[0].Equals("Field", StringComparison.InvariantCultureIgnoreCase)) {
    using(var lgp = new Ficedula.FF7.LGPFile(args[1])) {
        using(var ffile = lgp.Open(args[2])) {
            var field = new Ficedula.FF7.Field.FieldFile(ffile);
            var palettes = field.GetPalettes();
            var walkmesh = field.GetWalkmesh();
            var etables = field.GetEncounterTables();
            var cameras = field.GetCameraMatrices();
            var tg = field.GetTriggersAndGateways();
            var background = field.GetBackground();
            foreach(var layer in background.Export()) {
                File.WriteAllBytes(
                    @$"C:\temp\layer{layer.Layer}_{layer.Key}.png",
                    layer.Bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100).ToArray()
                );
            }

            var de = field.GetDialogEvent();
            var models = field.GetModels();
        }
    }
}