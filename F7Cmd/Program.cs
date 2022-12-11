// See https://aka.ms/new-console-template for more information
using Ficedula.FF7.Exporters;

Console.WriteLine("F7Cmd");
/*
foreach(string file in Directory.GetFiles(@"C:\temp\wm_us", "*.a")) {
    using (var fs = File.OpenRead(file)) {
        var anim = new Ficedula.FF7.Field.FieldAnim(fs);
        Console.WriteLine($"{file}: {anim.BoneCount} bones, {anim.Frames.Count} frames");
    }
}
foreach (string file in Directory.GetFiles(@"C:\temp\wm_us", "*.hrc")) {
    var hrc = new Ficedula.FF7.Field.HRCModel(
        s => File.OpenRead(Path.Combine(@"C:\temp\wm_us", s)),
        Path.GetFileName(file)
    );
    Console.WriteLine($"{file}: {hrc.Name}, {hrc.Bones.Count} bones");
}
*/

using(var l = new Ficedula.FF7.LGPFile(@"C:\games\FF7\data\menu\menu_us.lgp")) {
    foreach(int i in Enumerable.Range(0, 4)) {
        char c = (char)('a' + i);
        var t = new Ficedula.FF7.TexFile(l.Open($"btl_win_{c}_h.tex"));
        foreach (int p in Enumerable.Range(0, t.Palettes.Count)) {
            File.WriteAllBytes(
                $@"C:\temp\B{c}{p}.png",
                t.ToBitmap(p).Encode(SkiaSharp.SKEncodedImageFormat.Png, 100).ToArray()
            );
        }
    }
}

var tex = new Ficedula.FF7.TexFile(File.OpenRead(@"C:\temp\wm\wm_kumo.tex"));

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

if (args[0].Equals("BattleScene", StringComparison.InvariantCultureIgnoreCase)) {
    using(var fs = new FileStream(args[1], FileMode.Open, FileAccess.Read)) {
        foreach(var scene in Ficedula.FF7.Battle.SceneDecoder.Decode(fs)) {
            Console.WriteLine($"Formation {scene.FormationID} with {scene.Enemies.Count} enemies, location {Ficedula.FF7.Battle.SceneDecoder.LocationIDToFileName(scene.LocationID)}");
            Console.WriteLine(string.Join(",", scene.Enemies.Select(e => e.Enemy.Name)));
        }
    }
}
if (args[0].Equals("Kernel", StringComparison.OrdinalIgnoreCase)) {
    using (var fs = new FileStream(args[1], FileMode.Open, FileAccess.Read)) {
        var kernel = new Ficedula.FF7.Kernel(fs);

        var materia = new Ficedula.FF7.MateriaCollection(kernel);

        var armour = new Ficedula.FF7.ArmourCollection(kernel);

        var weapons = new Ficedula.FF7.WeaponCollection(kernel);

        var accessories = new Ficedula.FF7.AccessoryCollection(kernel);

        var items = new Ficedula.FF7.ItemCollection(kernel);

        File.WriteAllBytes(@"C:\temp\s9.bin", kernel.Sections.ElementAt(9));
        File.WriteAllBytes(@"C:\temp\s16.bin", kernel.Sections.ElementAt(16));

        var attacks = new Ficedula.FF7.Battle.AttackCollection(new MemoryStream(kernel.Sections.ElementAt(1)));

        var txt = new Ficedula.FF7.KernelText(kernel.Sections.ElementAt(19));
        Console.WriteLine(txt.Get(0));
        Console.WriteLine(txt.Get(1));
    }
}

if (args[0].Equals("Sounds", StringComparison.OrdinalIgnoreCase)) {
    using (var audio = new Ficedula.FF7.Audio(Path.Combine(args[1], "audio.dat"), Path.Combine(args[1], "audio.fmt"))) {
        Console.WriteLine($"Audio file with {audio.EntryCount} entries");

        foreach(int i in Enumerable.Range(0, audio.EntryCount)) {
            try {
                var ms = new MemoryStream();
                audio.Export(i, ms);
                File.WriteAllBytes(Path.Combine(args[2], $"{i}.wav"), ms.ToArray());
            } catch { }
            //
        }

        //File.WriteAllBytes(@"C:\temp\tff.raw", audio.ExportPCM(10, out int freq, out int chans));
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
            Console.WriteLine(field.GetDialogEvent().AkaoMusicIDs.Count);
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