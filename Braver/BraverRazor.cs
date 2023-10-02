// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.UI.Layout;
using Ficedula;
using RazorEngineCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {

    public interface IBraverTemplateModel {
        public FGame Game { get; }
        public string SourceCategory { get; }
        public string SourceExtension { get; }
    }
    public class BraverTemplate : RazorEngineTemplateBase {
        public FGame GameModel {
            get {
                switch (Model) {
                    case FGame game:
                        return game;
                    case IBraverTemplateModel model:
                        return model.Game;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        public string Bool(bool b) {
            return b.ToString().ToLower();
        }

        public string Include(string templateName, object model) {
            var btemplate = Model as IBraverTemplateModel;
            var cache = GameModel.Singleton(() => new RazorLayoutCache(GameModel));
            return cache.ApplyPartial(btemplate.SourceCategory, templateName + "." + btemplate.SourceExtension, false, model);
        }
    }

    internal class RazorLayoutCache {

        private Dictionary<string, IRazorEngineCompiledTemplate<BraverTemplate>> _templates = new Dictionary<string, IRazorEngineCompiledTemplate<BraverTemplate>>(StringComparer.InvariantCultureIgnoreCase);
        private IRazorEngine _razorEngine = new RazorEngine();
        private FGame _game;

        public FGame Game => _game;

        public RazorLayoutCache(FGame game) {
            _game = game;
        }

        public IRazorEngineCompiledTemplate<BraverTemplate> Compile(string category, string razorFile, bool forceReload) {
            string key = category + "\\" + razorFile;
            if (forceReload || !_templates.TryGetValue(key, out var razor)) {
                string template = _game.OpenString(category, razorFile);
                _templates[key] = razor = _razorEngine.Compile<BraverTemplate>(template, builder => {
                    builder.AddAssemblyReference(typeof(RazorLayoutCache));
                    builder.AddAssemblyReference(typeof(SaveData));
                    builder.AddAssemblyReference(typeof(Ficedula.FF7.Item));
                    builder.AddAssemblyReference(typeof(Enumerable));
                });
            }
            return razor;
        }

        public string ApplyPartial(string category, string razorFile, bool forceReload, object model) {
            return Compile(category, razorFile, forceReload).Run(template => {
                template.Model = model;
            });
        }
    }

}
