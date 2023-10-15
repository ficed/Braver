// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Braver {
    public static class AutoDispose {

        private static HashSet<Type> _exclude = new HashSet<Type> {
            typeof(GraphicsDevice)
        };

        private static Dictionary<Type, List<Action<object>>> _disposers = new();

        private static Action<object> DisposeForType(Type t) {
            if (_exclude.Contains(t)) return null;

            if (t.IsAssignableTo(typeof(AutoDispose)))
                return Dispose;

            if (t.IsAssignableTo(typeof(IDisposable)))
                return obj => (obj as IDisposable).Dispose();

            if (t.IsConstructedGenericType) {
                if (t.GetGenericTypeDefinition() == typeof(List<>)) {
                    var disposer = DisposeForType(t.GenericTypeArguments[0]);
                    if (disposer != null)
                        return obj => {
                            foreach (object item in (obj as System.Collections.IList))
                                disposer(item);
                        };
                } else if (t.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
                    var disposer = DisposeForType(t.GenericTypeArguments[0]);
                    if (disposer != null) {
                        return obj => {
                            foreach (object key in (obj as System.Collections.IDictionary).Keys)
                                disposer(key);
                        };
                    }
                    disposer = DisposeForType(t.GenericTypeArguments[1]);
                    if (disposer != null) {
                        return obj => {
                            foreach (object value in (obj as System.Collections.IDictionary).Values)
                                disposer(value);
                        };
                    }
                }
            }

            return null;
        }

        private static List<Action<object>> Build(Type t) {
            List<Action<object>> disposers = new();
            foreach (var field in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)) {
                var disposer = DisposeForType(field.FieldType);
                if (disposer != null) {
                    Trace.WriteLine($"Type {t.Name}: will auto dispose field {field.Name}");
                    disposers.Add(obj => {
                        var f = field.GetValue(obj);
                        if (f != null)
                            disposer(f);
                    });
                }
            }
            return disposers;
        }

        public static void Dispose(object o) {
            List<Action<object>> actions;
            lock (_disposers) {
                Type t = o.GetType();
                if (!_disposers.TryGetValue(t, out actions))
                    _disposers[t] = actions = Build(t);
            }
            foreach (var action in actions)
                action(o);
        }
    }

    public interface IAutoDispose { }
}
