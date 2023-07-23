// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Plugins {

    [AttributeUsage(AttributeTargets.Property)]
    public class ConfigPropertyAttribute : Attribute { 
        public string Name { get; set; }
        public string Description { get; set; }

        public ConfigPropertyAttribute(string name) {
            Name = name;
        }
        public ConfigPropertyAttribute(string name, string description) {
            Name = name;
            Description = description;
        }
    }

}
