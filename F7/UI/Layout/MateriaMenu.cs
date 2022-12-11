using Ficedula.FF7;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI.Layout {

    public class AvailableMateria {
        public int AP { get; set; }
        public Materia Materia { get; set; }

        public int Level => Materia.APLevels.TakeWhile(ap => ap <= AP).Count();
        public int? ToNextLevel {
            get {
                var next = Materia.APLevels.FirstOrDefault(ap => ap > AP);
                return next == 0 ? null : next - AP;
            }
        }
    }

    public class MateriaMenu : LayoutModel {

        public Group gMain;

        public List lbMateria;

        public Label lCheck, lArrange;

        public override bool IsRazorModel => true;

        public Character Character => _game.SaveData.Party[(int)_screen.Param];
        public Weapon Weapon => Character.GetWeapon(_game);
        public Armour Armour => Character.GetArmour(_game);

        public AvailableMateria CurrentMateria { get; private set; }

        private MagicText _magicText;

        public IEnumerable<AvailableMateria> AvailableMateria {
            get => Game.SaveData
                .MateriaStock
                .Select(m => m == null ? null : new UI.Layout.AvailableMateria {
                    AP = m.AP,
                    Materia = _materias[m.MateriaID],
                });
        }

        public IReadOnlyList<AvailableMateria> WeaponMateria {
            get => Character.WeaponMateria
                .Select(m => m == null ? null : new UI.Layout.AvailableMateria {
                    AP = m.AP,
                    Materia = _materias[m.MateriaID],
                })
                .ToList();
        }
        public IReadOnlyList<AvailableMateria> ArmourMateria {
            get => Character.ArmourMateria
                .Select(m => m == null ? null : new UI.Layout.AvailableMateria {
                    AP = m.AP,
                    Materia = _materias[m.MateriaID],
                })
                .ToList();
        }

        private Materias _materias;

        public override void Created(FGame g, LayoutScreen screen) {
            base.Created(g, screen);
            _materias = _game.Singleton<Materias>();
            _magicText = _game.Singleton<MagicText>();
        }

        protected override void OnInit() {
            base.OnInit();
            if (Focus == null) {
                PushFocus(gMain, lCheck);
            }
        }

        public override void CancelPressed() {
            if (FocusGroup == gMain) {
                _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
                InputEnabled = false;
                _screen.FadeOut(() => _game.PopScreen(_screen));
            } else
                base.CancelPressed();
        }

        public string MateriaColor(Materia m) {
            switch (m) {
                case MagicMateria:
                    return "magic";
                case CommandMateria:
                    return "command";
                default:
                    return "summon";
            }
        }

        public void Arrange_Click(Label L) {

        }
        public void Check_Click(Label L) {

        }

        public void EClick(Image i) {
        }
        public void EFocussed(Image i) {
            bool isWeapon = i.ID.StartsWith("W");
            int slot = int.Parse(i.ID.Substring(1));
            if (isWeapon)
                CurrentMateria = WeaponMateria[slot];
            else
                CurrentMateria = ArmourMateria[slot];
            _screen.Reload();
        }

        public void MateriaSelected(Label L) {

        }

        public void MateriaFocussed() {

        }


        public IEnumerable<string> MateriaAbilities(Materia m) {
            switch(m) {
                case MagicMateria magic:
                    return magic.Magics
                        .Where(m => m != null)
                        .Select(m => _magicText[m.Value].Name);
                default:
                    return Enumerable.Empty<string>();
            }
        }

        public IEnumerable<MateriaEffect> EquipEffects(Materia m) {
            if (m.EquipEffect.Strength != 0)
                yield return new MateriaEffect("Strength", m.EquipEffect.Strength, false);
            if (m.EquipEffect.Vitality != 0)
                yield return new MateriaEffect("Vitality", m.EquipEffect.Vitality, false);
            if (m.EquipEffect.Magic != 0)
                yield return new MateriaEffect("Magic", m.EquipEffect.Magic, false);
            if (m.EquipEffect.Spirit != 0)
                yield return new MateriaEffect("Spirit", m.EquipEffect.Spirit, false);
            if (m.EquipEffect.Dexterity != 0)
                yield return new MateriaEffect("Dexterity", m.EquipEffect.Dexterity, false);
            if (m.EquipEffect.Luck != 0)
                yield return new MateriaEffect("Luck", m.EquipEffect.Luck, false);
            if (m.EquipEffect.MaxHP != 0)
                yield return new MateriaEffect("MaxHP", m.EquipEffect.MaxHP, true);
            if (m.EquipEffect.MaxMP != 0)
                yield return new MateriaEffect("MaxMP", m.EquipEffect.MaxMP, true);
        }
    }

    public class MateriaEffect {
        public string Name { get; private set; }
        public int Change { get; private set; }
        public bool Percent { get; private set; }

        public MateriaEffect(string name, int change, bool percent) {
            Name = name;
            Change = change;
            Percent = percent;
        }
    }
}