// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System.Reflection;
using System.Runtime.Loader;
using System.Xml.Serialization;

namespace Braver.Plugins {
    public abstract class Plugin {
        public abstract string Name { get; }
        public abstract Version Version { get; }
        public abstract object ConfigObject { get; }
        public abstract IEnumerable<Type> GetPluginInstances();
        public abstract IEnumerable<IPluginInstance> Get(string context, Type t);

        public abstract void Init(BGame game);
    }

    public abstract class AutoEnabledPlugin : Plugin { }

    public interface IPluginInstance {
    }

    public class PluginConfigVar {
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public string Value { get; set; }
    }
    public class PluginConfig {
        [XmlElement("Var")]
        public List<PluginConfigVar> Vars { get; set; } = new();
        public string PluginClass { get; set; }
        public int Priority { get; set; } = 1000;
        public bool Enabled { get; set; }
    }

    public class PluginConfigs {
        [XmlElement("Plugin")]
        public List<PluginConfig> Configs { get; set; } = new();
    }

    public class PluginManager {

        public static IEnumerable<Plugin> GetPluginsFromAssembly(string dllFile) {
            var assembly = Assembly.LoadFrom(dllFile);
            return assembly.GetTypes()
                .Where(t => t.IsAssignableTo(typeof(Plugin)))
                .Select(t => Activator.CreateInstance(t))
                .OfType<Plugin>();
        }

        private Dictionary<Type, List<Plugin>> _types = new();

        public PluginManager() {
            var pluginTypes = System.Reflection.Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsAssignableTo(typeof(IPluginInstance)));
            foreach (var type in pluginTypes)
                _types[type] = new List<Plugin>();
        }

        private void Configure(Plugin p, PluginConfig config) {

            void DoConfigure(object o, string prefix) {
                if (o == null)
                    return;

                foreach (var prop in o.GetType().GetProperties()) {

                    if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string)) {
                        DoConfigure(prop.GetValue(o), prefix + prop.Name + ".");
                    } else {
                        var cvar = config.Vars.Find(v => v.Name == prefix + prop.Name);
                        if (cvar != null) {
                            if (prop.PropertyType == typeof(string))
                                prop.SetValue(o, cvar.Value);
                            else if (prop.PropertyType == typeof(bool))
                                prop.SetValue(o, bool.Parse(cvar.Value));
                            else if (prop.PropertyType == typeof(int))
                                prop.SetValue(o, int.Parse(cvar.Value));
                            else if (prop.PropertyType == typeof(float))
                                prop.SetValue(o, float.Parse(cvar.Value));
                            else if (prop.PropertyType == typeof(double))
                                prop.SetValue(o, double.Parse(cvar.Value));
                            else if (prop.PropertyType.IsEnum)
                                prop.SetValue(o, Enum.Parse(prop.PropertyType, cvar.Value));
                            else
                                throw new NotImplementedException();
                        }
                    }
                }
            }

            DoConfigure(p.ConfigObject, "");
        }

        public void Init(BGame game, IEnumerable<Plugin> plugins, PluginConfigs configs) {

            var configuredPlugins = plugins
                .Select(pl => new {
                    Plugin = pl,
                    Config = configs.Configs.FirstOrDefault(cfg => cfg.PluginClass == pl.GetType().FullName) ?? new PluginConfig(),
                })
                .Where(pl => pl.Config.Enabled || (pl.Plugin is AutoEnabledPlugin))
                .OrderBy(pl => pl.Config.Priority);

            foreach(var p in configuredPlugins) {
                System.Diagnostics.Trace.WriteLine($"Loading plugin {p.Plugin.Name} v{p.Plugin.Version}");
                Configure(p.Plugin, p.Config);
                p.Plugin.Init(game);
                foreach(var type in p.Plugin.GetPluginInstances()) {
                    _types[type].Add(p.Plugin);
                }
            }
        }

        public PluginInstances<T> GetInstances<T>(string context) where T : IPluginInstance {
            var instances = _types[typeof(T)].SelectMany(plugin => plugin.Get(context, typeof(T)));
            return new PluginInstances<T>(instances.Cast<T>());
        }
    }


    public class PluginInstances<T> : IDisposable where T : IPluginInstance {

        private List<T> _instances;
        internal PluginInstances(IEnumerable<T> instances) {
            _instances = instances.ToList();
        }

        public void Call(Action<T> action) {
            foreach (var instance in _instances.OfType<T>())
                action(instance);
        }
        public U? Call<U>(Func<T, U> fetch) where U : class {
            foreach (var instance in _instances.OfType<T>().Reverse()) {
                U result = fetch(instance);
                if (result != null) return result;
            }
            return null;
        }
        public IEnumerable<U> CallAll<U>(Func<T, U> fetch) {
            return _instances
                .OfType<T>()
                .Reverse()
                .Select(t => fetch(t));
        }

        public void Dispose() {
            foreach (var instance in _instances.OfType<IDisposable>())
                instance.Dispose();
        }
    }
}