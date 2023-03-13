// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

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

        public bool IsSaveMenu => ((bool?)_screen.Param).GetValueOrDefault(true);

        public List<SaveEntry> Entries { get; } = new();

        public List lbSaves;

        public override void Created(FGame g, LayoutScreen screen) {
            base.Created(g, screen);

            foreach(int slot in Enumerable.Range(0, 9)) {
                string path = Path.Combine(g.GetPath("save"), $"save{slot}");
                string sav = path + ".sav";
                if (File.Exists(sav)) {
                    SaveData saveData;
                    using (var fs = File.OpenRead(path + ".sav")) {
                        if (Pack.IsPack(fs)) {
                            var pack = new Pack(fs);
                            using (var data = pack.Read("SaveData"))
                                saveData = Serialisation.Deserialise<SaveData>(data);
                        } else {
                            saveData = Serialisation.Deserialise<SaveData>(fs);
                        }
                    }
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
            if (IsSaveMenu) {
                _game.Save(path, !_game.DebugOptions.SeparateSaveFiles);
                InputEnabled = false;
                _screen.FadeOut(() => _game.PopScreen(_screen));
            } else {
                InputEnabled = false;
                _screen.FadeOut(() => {
                    _game.PopScreen(_screen);
                    _game.Load(path);
                });
            }
        }
    }
}
