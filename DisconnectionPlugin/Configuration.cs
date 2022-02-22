﻿// 
// Configuration.cs
// 
// Copyright (c) 2019 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#nullable enable

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace DisconnectionPlugin
{
    public enum DisconnectType
    {
        BLIPErrorMessage,
        WebsocketClose,
        PipeBreak,
        Timeout
    }

    [UsedImplicitly]
    public sealed class Configuration
    {
        [DefaultValue(DisconnectType.PipeBreak)]
        [UsedImplicitly]
        public DisconnectType DisconnectType { get; set; }

        [UsedImplicitly]
        [Required]
        public string[] PatternClauses { get; set; } = Array.Empty<string>();
    }
}
