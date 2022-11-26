using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Braver.UI.Layout {

    public class SaveEntry {
        public string File { get; set; }
        public string Location { get; set; }
        public DateTime? Timestamp { get; set; }

    }

    public class SaveMenu : LayoutModel {

        public override bool IsRazorModel => true;

        public List<SaveEntry> Entries { get; } = new();

        public List lbSaves;

        public override void Created(FGame g, LayoutScreen screen) {
            base.Created(g, screen);

            foreach(int slot in Enumerable.Range(0, 9)) {
                string path = Path.Combine(FGame.GetSavePath(), $"save{slot}");
                string sav = path + ".sav";
                if (File.Exists(sav)) {
                    var saveData = Serialisation.Deserialise<SaveData>(File.ReadAllText(sav));
                    Entries.Add(new SaveEntry {
                        Location = saveData.Location,
                        Timestamp = File.GetLastWriteTime(sav),
                        File = path,
                    });
                } else {
                    Entries.Add(new SaveEntry {
                        Location = "(Empty slot)",
                        File = path,
                    });
                }
            }
        }

        protected override void OnInit() {
            base.OnInit();
            if (Focus == null) {
                PushFocus(lbSaves, lbSaves.Children[0]);
            }
        }

        public override void CancelPressed() {
            if (FocusGroup == lbSaves) {
                _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
                InputEnabled = false;
                _screen.FadeOut(() => _game.PopScreen(_screen));
            } else
                base.CancelPressed();
        }

        public void SaveSelected(Box bSave) {
            string path = bSave.ID;
            _game.Save(path);
            InputEnabled = false;
            _screen.FadeOut(() => _game.PopScreen(_screen));
        }
    }
}
