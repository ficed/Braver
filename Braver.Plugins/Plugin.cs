// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System.Xml.Serialization;

namespace Braver.Plugins {
    public abstract class Plugin {
        public abstract string Name { get; }
        public abstract Version Version { get; }
        public abstract object ConfigObject { get; }
        public abstract IEnumerable<Type> GetPluginInstances();
        public abstract IPluginInstance Get(string context, Type t);

        public abstract void Init(BGame game);
    }

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
        public bool Enabled { get; set; } = true;
    }

    public class PluginConfigs {
        [XmlElement("Plugin")]
        public List<PluginConfig> Configs { get; set; } = new();
    }

    public class PluginManager {

        private Dictionary<Type, List<Plugin>> _types = new();

        public PluginManager() {
            var pluginTypes = System.Reflection.Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.IsAssignableTo(typeof(IPluginInstance)));
            foreach (var type in pluginTypes)
                _types[type] = new List<Plugin>();
        }

        private void Configure(Plugin p, PluginConfig config) {
            var obj = p.ConfigObject;
            if (obj == null)
                return;
            foreach(var prop in obj.GetType().GetProperties()) {
                var cvar = config.Vars.Find(v => v.Name == prop.Name);
                if (cvar != null) {
                    if (prop.PropertyType == typeof(string))
                        prop.SetValue(obj, cvar.Value);
                    else if (prop.PropertyType == typeof(int))
                        prop.SetValue(obj, int.Parse(cvar.Value));
                    else if (prop.PropertyType == typeof(float))
                        prop.SetValue(obj, float.Parse(cvar.Value));
                    else if (prop.PropertyType == typeof(double))
                        prop.SetValue(obj, double.Parse(cvar.Value));
                    else if (prop.PropertyType.IsEnum)
                        prop.SetValue(obj, Enum.Parse(prop.PropertyType, cvar.Value));
                    else
                        throw new NotImplementedException();
                }
            }
        }

        public void Init(BGame game, IEnumerable<Plugin> plugins, PluginConfigs configs) {

            var configuredPlugins = plugins
                .Select(pl => new {
                    Plugin = pl,
                    Config = configs.Configs.FirstOrDefault(cfg => cfg.PluginClass == pl.GetType().FullName) ?? new PluginConfig(),
                })
                .Where(pl => pl.Config.Enabled)
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

        public PluginInstances GetInstances(string context, params Type[] types) {
            var instances = types
                .SelectMany(t => _types[t].Select(plugin => plugin.Get(context, t)));
            return new PluginInstances(instances);
        }
    }

    public class PluginInstances : IDisposable {

        private List<IPluginInstance> _instances;
        internal PluginInstances(IEnumerable<IPluginInstance> instances) {
            _instances = instances.ToList();
        }

        public void Call<T>(Action<T> action) where T : IPluginInstance {
            foreach (var instance in _instances.OfType<T>())
                action(instance);
        }

        public void Dispose() {
            foreach (var instance in _instances.OfType<IDisposable>())
                instance.Dispose();
        }
    }
}