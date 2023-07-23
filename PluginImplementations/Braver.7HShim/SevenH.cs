// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using Braver.Plugins.UI;
using IrosArchive;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Xml;

namespace Braver._7HShim {

    public class SevenHConfig {
        protected Dictionary<string, int> _settings = new(StringComparer.InvariantCultureIgnoreCase);

        protected void SetBool(string name, bool b) => _settings[name] = b ? 1 : 0;
        protected bool GetBool(string name) => _settings.GetValueOrDefault(name) != 0;
        protected void SetInt(string name, int i) => _settings[name] = i;
        protected int GetInt(string name) => _settings.GetValueOrDefault(name);

        private static char[] OP_CHARS = new[] { '=', '<', '>', '!' };
        internal bool Evaluate(string comparison) {
            int opStart = comparison.IndexOfAny(OP_CHARS),
                opEnd = comparison.LastIndexOfAny(OP_CHARS);
            string operand0 = comparison.Substring(0, opStart).Trim(),
                operand1 = comparison.Substring(opEnd + 1).Trim(),
                op = comparison.Substring(opStart, opEnd - opStart + 1).Trim();

            if (string.IsNullOrEmpty(operand0) || string.IsNullOrEmpty(operand1) || string.IsNullOrEmpty(op))
                throw new InvalidDataException($"Bad comparison: {comparison}");

            int val1 = _settings.GetValueOrDefault(operand0),
                val2 = int.Parse(operand1);

            if (op == "=")
                return val1 == val2;
            else if (op == "!=")
                return val1 != val2;
            else if (op == "<")
                return val1 < val2;
            else if (op == ">")
                return val1 > val2;
            else if (op == "<=")
                return val1 <= val2;
            else if (op == ">=")
                return val1 >= val2;
            else
                throw new InvalidDataException($"Bad comparison: {comparison}");
        }

        internal bool Evaluate(XmlNode condition) {
            if (condition.LocalName.Equals("Not", StringComparison.InvariantCultureIgnoreCase))
                return !Evaluate(condition.ChildNodes[0]);
            else if (condition.LocalName.Equals("And", StringComparison.InvariantCultureIgnoreCase))
                return Evaluate(condition.ChildNodes[0]) && Evaluate(condition.ChildNodes[1]);
            else if (condition.LocalName.Equals("Or", StringComparison.InvariantCultureIgnoreCase))
                return Evaluate(condition.ChildNodes[0]) || Evaluate(condition.ChildNodes[1]);
            else if (condition.LocalName.Equals("Option", StringComparison.InvariantCultureIgnoreCase))
                return Evaluate(condition.InnerText);
            else
                throw new InvalidDataException($"Bad XML condition: {condition.OuterXml}");
        }

        public static SevenHConfig Build(XmlNode modinfo, ModuleBuilder module) {
            var id = Guid.Parse(modinfo.SelectSingleNode("ID").InnerText);
            var typ = module.DefineType("Config" + id.ToString("N"), TypeAttributes.Class | TypeAttributes.Public, typeof(SevenHConfig));

            var mGetB = typeof(SevenHConfig).GetMethod(nameof(GetBool), BindingFlags.NonPublic | BindingFlags.Instance);
            var mSetB = typeof(SevenHConfig).GetMethod(nameof(SetBool), BindingFlags.NonPublic | BindingFlags.Instance);
            var mGetI = typeof(SevenHConfig).GetMethod(nameof(GetInt), BindingFlags.NonPublic | BindingFlags.Instance);
            var mSetI = typeof(SevenHConfig).GetMethod(nameof(SetInt), BindingFlags.NonPublic | BindingFlags.Instance);

            void DoProp(string propName, Type propType, string name, string description) {

                MethodInfo mGet = propType == typeof(bool) ? mGetB : mGetI,
                    mSet = propType == typeof(bool) ? mSetB : mSetI;

                var prop = typ.DefineProperty(
                    propName,
                    PropertyAttributes.None,
                    propType, null
                );
                var attr = new CustomAttributeBuilder(
                    typeof(ConfigPropertyAttribute).GetConstructor(new[] { typeof(string), typeof(string) }),
                    new object[] { name, description }
                );
                prop.SetCustomAttribute(attr);
                var getBuild = typ.DefineMethod(
                    "get_" + prop.Name,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                    propType, null
                );
                var getIL = getBuild.GetILGenerator();
                getIL.Emit(OpCodes.Ldarg_0);
                getIL.Emit(OpCodes.Ldstr, prop.Name);
                getIL.Emit(OpCodes.Callvirt, mGet);
                //getIL.Emit(OpCodes.Castclass, propType);
                getIL.Emit(OpCodes.Ret);
                prop.SetGetMethod(getBuild);

                var setBuild = typ.DefineMethod(
                    "set_" + prop.Name,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                    null, new[] { propType }
                );
                var setIL = setBuild.GetILGenerator();
                setIL.Emit(OpCodes.Ldarg_0);
                setIL.Emit(OpCodes.Ldstr, prop.Name);
                setIL.Emit(OpCodes.Ldarg_1);
                if (propType.IsEnum)
                    setIL.Emit(OpCodes.Conv_I4);
                setIL.Emit(OpCodes.Callvirt, mSet);
                setIL.Emit(OpCodes.Ret);
                prop.SetSetMethod(setBuild);
            }

            var defaults = new Dictionary<string, int>();
            foreach (XmlNode xoption in modinfo.SelectNodes("ConfigOption")) {
                string optType = xoption.SelectSingleNode("Type").InnerText;
                string optID = xoption.SelectSingleNode("ID").InnerText;
                string optName = xoption.SelectSingleNode("Name").InnerText;
                string optDesc = xoption.SelectSingleNode("Description").InnerText;
                string def = xoption.SelectSingleNode("Default")?.InnerText;
                if (optType.Equals("Bool", StringComparison.InvariantCultureIgnoreCase)) {
                    DoProp(optID, typeof(bool), optName, optDesc);
                } else if (optType.Equals("List", StringComparison.InvariantCultureIgnoreCase)) {
                    var enumtype = module.DefineEnum("List_" + optID, TypeAttributes.Public, typeof(int));
                    foreach(XmlNode xlistopt in xoption.SelectNodes("Option")) {
                        enumtype.DefineLiteral(
                            xlistopt.Attributes["Name"].Value.Replace(" ", "_"),
                            int.Parse(xlistopt.Attributes["Value"].Value)
                        );
                    }
                    var builtEnum = enumtype.CreateType();
                    DoProp(optID, builtEnum, optName, optDesc);
                }
                if (def != null)
                    defaults[optID] = int.Parse(def);
            }

            var built = typ.CreateType();
            var config = (SevenHConfig)Activator.CreateInstance(built);
            foreach (var def in defaults)
                config._settings[def.Key] = def.Value;
            return config;
        }
    }

    internal abstract class SevenHMod {
        public XmlDocument ModXml { get; protected set; }
        public int ID { get; set; }
        public SevenHConfig Config { get; set; }

        public abstract IEnumerable<string> GetSubFolders(string folder);
        public abstract IEnumerable<string> GetFolders();
        public abstract DataSource GetDataSource(string folder, string subfolder);
    }

    internal class SevenHIroMod : SevenHMod, IDisposable {
        private IrosArchive.IrosArc _iro;

        public SevenHIroMod(string iroFile) {
            _iro = new IrosArchive.IrosArc(iroFile);

            var doc = new XmlDocument();
            using (var s = _iro.GetData("mod.xml"))
                doc.Load(s);
            ModXml = doc;
        }

        private class IroDataSource : DataSource {
            private IrosArchive.IrosArc _iro;
            private string _folder;
            private string[] _filenames;

            public IroDataSource(IrosArc iro, string folder) {
                _iro = iro;
                _folder = folder;
                _filenames = iro.AllFileNames()
                    .Where(s => s.StartsWith(folder))
                    .Select(s => Path.GetFileName(s))
                    .ToArray();
            }

            public override IEnumerable<string> Scan() => _filenames;
            public override Stream TryOpen(string file) => _iro.GetData(_folder + file);

            public override string ToString() => $"IRO {_iro}";
        }

        public override DataSource GetDataSource(string folder, string subfolder) {
            if (string.IsNullOrWhiteSpace(subfolder))
                return new IroDataSource(_iro, folder + "\\");
            else
                return new IroDataSource(_iro, folder + "\\" + subfolder + "\\");
        }

        public override IEnumerable<string> GetFolders() {
            return _iro.AllFolderNames()
                .Select(s => {
                    int separator = s.IndexOf('\\');
                    if (separator >= 0)
                        return s.Substring(0, separator);
                    else
                        return s;
                })
                .Distinct();
        }

        public override IEnumerable<string> GetSubFolders(string folder) {
            string prefix = folder + "\\";
            return _iro.AllFolderNames()
                .Where(s => s.StartsWith(prefix))
                .Select(s => s.Substring(prefix.Length))
                .Where(s => !s.Contains('\\'));
        }

        public void Dispose() {
            _iro.Dispose();
        }
    }

    internal class SevenHFileMod : SevenHMod {
        private string _root;

        public SevenHFileMod(string root) {
            _root = root;
            var doc = new XmlDocument();
            doc.Load(Path.Combine(_root, "mod.xml"));
            ModXml = doc;
        }

        public override DataSource GetDataSource(string folder, string subfolder) {
            return new FileDataSource(Path.Combine(_root, folder, subfolder));
        }

        public override IEnumerable<string> GetFolders() {
            return Directory.GetDirectories(_root)
                .Select(fn => Path.GetFileName(fn));
        }

        public override IEnumerable<string> GetSubFolders(string folder) {
            return Directory.GetDirectories(Path.Combine(_root, folder))
                .Select(fn => Path.GetFileName(fn));
        }

    }

    public class SevenHConfigCollection {
        protected Dictionary<int, SevenHConfig> _configs = new();
        protected SevenHConfig GetConfig(int id) => _configs[id];

        internal static SevenHConfigCollection Build(ModuleBuilder module, IEnumerable<SevenHMod> mods) {
            var typ = module.DefineType("Configs", TypeAttributes.Class | TypeAttributes.Public, typeof(SevenHConfigCollection));

            Dictionary<int, SevenHConfig> configs = new();
            var mget = typeof(SevenHConfigCollection).GetMethod(nameof(GetConfig), BindingFlags.NonPublic | BindingFlags.Instance);

            foreach(var mod in mods) {
                var config = SevenHConfig.Build(mod.ModXml.SelectSingleNode("/ModInfo"), module);
                mod.Config = configs[mod.ID] = config;
                Guid modid = Guid.Parse(mod.ModXml.SelectSingleNode("/ModInfo/ID").InnerText);

                var prop = typ.DefineProperty(
                    "Config" + modid.ToString("N"),
                    PropertyAttributes.None,
                    config.GetType(), null
                );

                var attr = new CustomAttributeBuilder(
                    typeof(ConfigPropertyAttribute).GetConstructor(new[] { typeof(string), typeof(string) }),
                    new object[] { 
                        mod.ModXml.SelectSingleNode("/ModInfo/Name").InnerText,
                        mod.ModXml.SelectSingleNode("/ModInfo/Description").InnerText
                    }
                );
                prop.SetCustomAttribute(attr);

                var getBuild = typ.DefineMethod(
                    "get_" + prop.Name,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                    config.GetType(), null
                );
                var getIL = getBuild.GetILGenerator();
                getIL.Emit(OpCodes.Ldarg_0);
                getIL.Emit(OpCodes.Ldc_I4, mod.ID);
                getIL.Emit(OpCodes.Callvirt, mget);
                getIL.Emit(OpCodes.Castclass, config.GetType());
                getIL.Emit(OpCodes.Ret);
                prop.SetGetMethod(getBuild);
            }

            var built = typ.CreateType();
            var collection = (SevenHConfigCollection)Activator.CreateInstance(built);
            collection._configs = configs;
            return collection;
        }
    }

    /*
    public static class Tester {
        public static void Test() {
            AssemblyName assemblyName = new AssemblyName("SevenHShim.DynamicConfig");
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

            // Create a module builder
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("SevenHModule");

            var doc = new XmlDocument();
            doc.Load(@"C:\temp\Cosmo\mod.xml");
            var config = SevenHConfigCollection.Build(moduleBuilder, new[] { doc });

            foreach (var prop in config.GetType().GetProperties()) {
                Console.WriteLine(prop.Name);
            }
        }
    }
    */

    public class SevenH : Plugin {
        public override string Name => "7th Heaven compatibility wrapper";

        public override Version Version => new Version(0, 0, 1);

        public override object ConfigObject => _configs;

        private List<SevenHMod> _mods = new();
        private SevenHConfigCollection _configs;

        public SevenH() {

            string thisFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            foreach(string subdirectory in Directory.GetDirectories(thisFolder)) {
                string modxml = Path.Combine(subdirectory, "mod.xml");
                if (File.Exists(modxml)) {
                    var doc = new XmlDocument();
                    doc.Load(modxml);
                    _mods.Add(new SevenHFileMod(subdirectory) {
                        ID = _mods.Count + 1,
                    });
                }
            }
            foreach(string iro in Directory.GetFiles(thisFolder, "*.iro")) {
                _mods.Add(new SevenHIroMod(iro) {
                    ID = _mods.Count + 1
                });
            }

            AssemblyName assemblyName = new AssemblyName("SevenHShim.DynamicConfig");
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("SevenHModule");

            _configs = SevenHConfigCollection.Build(moduleBuilder, _mods);
        }

        public override IEnumerable<IPluginInstance> Get(string context, Type t) {
            throw new NotImplementedException();
        }

        public override IEnumerable<Type> GetPluginInstances() {
            yield break;
        }

        public override void Init(BGame game) {
            foreach(var mod in _mods) {
                HashSet<string> remainingFolders = new HashSet<string>(mod.GetFolders(), StringComparer.InvariantCultureIgnoreCase);
                foreach(XmlNode xfolder in mod.ModXml.SelectNodes("/ModInfo/ModFolder")) {
                    string folder = xfolder.Attributes["Folder"].Value;
                    remainingFolders.Remove(folder);
                    bool isActive;
                    if (xfolder.Attributes["ActiveWhen"] != null)
                        isActive = mod.Config.Evaluate(xfolder.Attributes["ActiveWhen"].Value);
                    else if (xfolder.SelectSingleNode("ActiveWhen") != null)
                        isActive = mod.Config.Evaluate(xfolder.SelectSingleNode("ActiveWhen").ChildNodes[0]);
                    else
                        isActive = true;
                    if (isActive) {
                        foreach (string datafolder in mod.GetSubFolders(folder))
                            game.AddDataSource(Path.GetFileName(datafolder), mod.GetDataSource(folder, datafolder));
                    }
                }
                foreach (string folder in remainingFolders)
                    game.AddDataSource(Path.GetFileName(folder), mod.GetDataSource(folder, ""));
            }
        }
    }

}