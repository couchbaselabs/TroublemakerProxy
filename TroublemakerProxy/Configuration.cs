// 
//  Configuration.cs
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace TroublemakerProxy
{

    /// <summary>
    /// The configuration class for the overall troublemaker proxy
    /// </summary>
    [UsedImplicitly]
    public sealed class Configuration
    {
        #region Properties

        /// <summary>
        /// The port to listen on for incoming connections
        /// </summary>
        [UsedImplicitly]
        [Required] 
        public int FromPort { get; set; }

        /// <summary>
        /// The plugins to load for use in this session
        /// </summary>
        [UsedImplicitly]
        public Plugin[]? Plugins { get; set; }

        /// <summary>
        /// The host to connect to for outgoing connections (default localhost)
        /// </summary>
        [UsedImplicitly]
        public string ToHost { get; set; } = "localhost";

        /// <summary>
        /// The port to connect to for outgoing connections
        /// </summary>
        [UsedImplicitly]
        [Required] 
        public int ToPort { get; set; }

        #endregion
    }

    /// <summary>
    /// A class describing an assembly containing an <see cref="TroublemakerInterfaces.ITroublemakerPlugin"/>
    /// implementation (or more).
    /// </summary>
    [UsedImplicitly]
    public sealed class Plugin
    {
        #region Properties

        /// <summary>
        /// The path to the configuration file for the plugin, if needed
        /// </summary>
        [UsedImplicitly]
        public string? ConfigPath { get; set; }

        /// <summary>
        /// The path to the assembly file containing the plugin class
        /// </summary>
        [UsedImplicitly]
        [Required]
        public string Path { get; set; } = String.Empty;

        #endregion
    }
}