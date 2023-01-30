using Braver.Battle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI.Layout {
    public class Name : LayoutModel {

        public Label lName, lReset, lOK;
        public Box Root;

        public override bool IsRazorModel => true;

        public int CharacterID { get; set; }

        //TODO - is there a default list we can read from the game files
        private static readonly string[] DEFAULT_NAMES = new[] {
            "Cloud", "Barret", "Tifa", "Aeris", "Red XIII", "Yuffie", "Cait Sith", "Vincent", "Cid",
        };

        public override void Created(FGame g, LayoutScreen screen) {
            base.Created(g, screen);
            CharacterID = (int)screen.Param;
        }

        protected override void OnInit() {
            base.OnInit();
            lName.Text = DEFAULT_NAMES[CharacterID];
            if (Focus == null)
                PushFocus(Root, lOK);
        }

        public void ResetClick() {
            lName.Text = DEFAULT_NAMES[CharacterID];
        }

        public void OKClick() {
            Game.SaveData.Characters[CharacterID].Name = lName.Text;
            Game.PopScreen(_screen);            
        }

        public void CharClick(Label L) {
            if (lName.Text.Length < 12)
                lName.Text += L.Text.Replace('_', ' ');
        }

        public override void CancelPressed() {
            //base.CancelPressed();
            if (lName.Text.Length > 0)
                lName.Text = lName.Text.Substring(0, lName.Text.Length - 1);
        }

    }
}
