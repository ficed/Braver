// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

namespace Braver.Plugins {
    public abstract class Plugin {
        public abstract string Name { get; }
        public abstract Version Version { get; }
        public abstract IEnumerable<Type> GetPluginInstances();
        public abstract IPluginInstance Get(string context, Type t);

        public abstract void Init(BGame game);
    }

    public interface IPluginInstance {
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

        public void Init(BGame game, IEnumerable<Plugin> plugins) { 
            foreach(var plugin in plugins) {
                System.Diagnostics.Trace.WriteLine($"Loading plugin {plugin.Name} v{plugin.Version}");
                plugin.Init(game);
                foreach(var type in plugin.GetPluginInstances()) {
                    _types[type].Add(plugin);
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