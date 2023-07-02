// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver;
using Braver.Plugins;
using Ficedula.FF7;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace BraverLauncher {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private PluginConfigs _pluginConfig;
        private string _pluginConfigPath, _braverConfigPath;

        private class LoadedPlugin {
            public Plugin Plugin { get; set; }

            public override string ToString() => Plugin.Name;
        }

        public MainWindow() {
            InitializeComponent();

            string root = Environment.GetCommandLineArgs()
                .Where(s => s.StartsWith("/root:"))
                .Select(s => s.Substring(6))
                .FirstOrDefault()
                ?? Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);

            _braverConfigPath = Path.Combine(root, "braver.cfg");
            string pluginPath = Environment.GetCommandLineArgs()
                .Where(s => s.StartsWith("/plugins:"))
                .Select(s => s.Substring(9))
                .FirstOrDefault()
                ?? Path.Combine(root, "plugins");
            _pluginConfigPath = Path.Combine(pluginPath, "config.xml");
            if (File.Exists(_pluginConfigPath))
                _pluginConfig = Serialisation.Deserialise<PluginConfigs>(File.ReadAllText(_pluginConfigPath));
            else
                _pluginConfig = new PluginConfigs();

            if (Directory.Exists(pluginPath)) {
                foreach (var folder in Directory.GetDirectories(pluginPath)) {
                    var plugins = Directory.GetFiles(folder, "*.dll")
                            .Select(fn => System.Reflection.Assembly.LoadFrom(fn))
                            .SelectMany(asm => asm.GetTypes())
                            .Where(t => t.IsAssignableTo(typeof(Plugin)))
                            .Select(t => Activator.CreateInstance(t))
                            .OfType<Plugin>();

                    foreach (var plugin in plugins) {
                        lbPlugins.Items.Add(new LoadedPlugin { Plugin = plugin });
                    }
                }
            }

            if (File.Exists(_braverConfigPath)) {
                Dictionary<string, string> settings = File.ReadAllLines(_braverConfigPath)
                    .Select(s => s.Split('='))
                    .ToDictionary(sa => sa[0], sa => sa[1], StringComparer.InvariantCultureIgnoreCase);
                txtFF7.Text = settings.GetValueOrDefault("FF7");
                txtMovies.Text = settings.GetValueOrDefault("Movies");
                txtSave.Text = settings.GetValueOrDefault("Save");
                if (txtSave.Text == ".") txtSave.Text = "";
                slMusicVolume.Value = double.Parse(settings.GetValueOrDefault("Options.MusicVolume") ?? "1") * 100;
            } 
        }

        private void DoBrowse(TextBox txt) {
            var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog {
                SelectedPath = txt.Text,
                Multiselect = false,
            };
            if (dlg.ShowDialog() ?? false) {
                txt.Text = dlg.SelectedPath;
            }
        }

        private void btnFF7_Click(object sender, RoutedEventArgs e) {
            DoBrowse(txtFF7);
        }

        private void btnMovies_Click(object sender, RoutedEventArgs e) {
            DoBrowse(txtMovies);
        }

        private void btnSave_Click(object sender, RoutedEventArgs e) {
            DoBrowse(txtSave);
        }

        private void lbPlugins_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (lbPlugins.SelectedItem == null) return;

            var plugin = lbPlugins.SelectedItem as LoadedPlugin;
            gPluginConfig.Children.Clear();
            gPluginConfig.RowDefinitions.Clear();
            if (plugin.Plugin.ConfigObject != null) {

                var config = _pluginConfig.Configs
                    .FirstOrDefault(pc => pc.PluginClass == plugin.Plugin.GetType().FullName);
                    
                if (config == null) {
                    config = new PluginConfig {
                        PluginClass = plugin.Plugin.GetType().FullName,
                    };
                    _pluginConfig.Configs.Add(config);
                }

                void DoAdd(string name, Control c) {
                    gPluginConfig.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                    if (!string.IsNullOrEmpty(name)) {
                        var lbl = new Label { Content = name };
                        Grid.SetRow(lbl, gPluginConfig.RowDefinitions.Count - 1);
                        gPluginConfig.Children.Add(lbl);
                    }
                    Grid.SetRow(c, gPluginConfig.RowDefinitions.Count - 1);
                    Grid.SetColumn(c, 1);
                    gPluginConfig.Children.Add(c);
                }

                void SetProp(string propName, string value) {
                    var v = config.Vars.FirstOrDefault(cv => cv.Name == propName);
                    if (v == null) {
                        v = new PluginConfigVar { Name = propName };
                        config.Vars.Add(v);
                    }
                    v.Value = value;
                }

                var enabled = new CheckBox { 
                    Content = "Enabled",
                    IsChecked = config.Enabled,
                };
                DoAdd("", enabled);
                enabled.Checked += (_o, _e) => config.Enabled = enabled.IsChecked ?? false;

                foreach (var prop in plugin.Plugin.ConfigObject.GetType().GetProperties()) {
                    var v = config.Vars.FirstOrDefault(cv => cv.Name == prop.Name);
                    if (prop.PropertyType == typeof(string)) {
                        TextBox tb = new TextBox {
                            Text = v.Value ?? prop.GetValue(plugin.Plugin.ConfigObject)?.ToString() ?? string.Empty,
                        };
                        DoAdd(prop.Name, tb);
                        tb.TextChanged += (_o, _e) => SetProp(prop.Name, tb.Text);
                    } else if (prop.PropertyType == typeof(bool)) {
                        CheckBox cb = new CheckBox {
                            Content = prop.Name,
                            IsChecked = (bool)prop.GetValue(plugin.Plugin.ConfigObject),
                        };
                        DoAdd("", cb);
                        cb.Checked += (_o, _e) => SetProp(prop.Name, (cb.IsChecked ?? false).ToString());
                    } else
                        throw new NotImplementedException();
                }
            }
        }

        private void btnLaunch_Click(object sender, RoutedEventArgs e) {
            if (!File.Exists(Path.Combine(txtFF7.Text, "FF7.exe"))) {
                MessageBox.Show("Cannot locate FF7.exe - check the path", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!File.Exists(Path.Combine(txtMovies.Text, "opening.mp4")) && !File.Exists(Path.Combine(txtMovies.Text, "opening.avi"))) {
                MessageBox.Show("Cannot locate opening movie - check the path", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string save = txtSave.Text;
            if (string.IsNullOrWhiteSpace(save))
                save = ".";

            File.WriteAllLines(_braverConfigPath, new[] {
                $"FF7={txtFF7.Text}",
                $"Movies={txtMovies.Text}",
                $"Save={save}",
                $"Braver=.",
                $"Plugins={Path.GetDirectoryName(_pluginConfigPath)}",
                $"BData={Path.Combine(Path.GetDirectoryName(_braverConfigPath), "Data.bpack")}",
                $"Options.MusicVolume={slMusicVolume.Value/100}"
            });

            using (var fs = new FileStream(_pluginConfigPath, FileMode.Create))
                Serialisation.Serialise(_pluginConfig, fs);

            System.Diagnostics.Process.Start(
                Path.Combine(Path.GetDirectoryName(_braverConfigPath), "Braver.exe")
            );
        }
    }
}
