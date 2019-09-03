// 
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

using TroublemakerInterfaces;

namespace MessageInterceptorPlugin
{
    public sealed class Configuration
    {
        #region Variables

        private readonly List<Rule> _rules;

        #endregion

        #region Properties

        public IReadOnlyList<Rule> Rules => _rules;

        #endregion

        #region Constructors

        public Configuration(List<Rule> rules)
        {
            _rules = rules;
        }

        #endregion

        #region Public Methods

        public void Used(Rule rule)
        {
            if (rule.Criteria.ApplicationFrequency == InputCriteria.Frequency.OnlyOnce) {
                _rules.Remove(rule);
            }
        }

        #endregion
    }

    public sealed class Rule
    {
        #region Properties

        public InputCriteria Criteria { get; }

        [JsonConverter(typeof(ListConverter<IOutputTransform, OutputTransformConverter>))]
        public IReadOnlyList<IOutputTransform> OutputTransforms { get; }

        [DefaultValue(Direction.ToClient | Direction.ToServer)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public Direction RuleDirection { get; }

        #endregion

        #region Constructors

        public Rule(InputCriteria criteria, IReadOnlyList<IOutputTransform> outputTransforms,
            Direction ruleDirection = Direction.ToClient | Direction.ToServer)
        {
            Criteria = criteria;
            OutputTransforms = outputTransforms;
            RuleDirection = ruleDirection;
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            string last;
            if (OutputTransforms.Count > 1) {
                last = OutputTransforms.Skip(1)
                    .Aggregate(OutputTransforms.First().ToString(), (x, y) => $"{x} and {y}", x => x);
            } else {
                last = OutputTransforms.First().ToString();
            }

            return $"While sending {RuleDirection}, when {Criteria}, {last}";
        }

        #endregion

        [Flags]
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Direction
        {
            ToServer = 1,
            ToClient = 1 << 1
        }
    }

    public sealed class ListConverter<TVal, TConverter> : JsonConverter where TConverter : JsonConverter
    {
        #region Properties

        public override bool CanWrite => false;

        #endregion

        #region Overrides

        public override bool CanConvert(Type objectType) => typeof(IReadOnlyList<TVal>).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.StartArray) {
                throw new InvalidDataException("Invalid list in JSON");
            }

            var converter = Activator.CreateInstance<TConverter>();
            var retVal = new List<TVal>();
            while (reader.TokenType != JsonToken.EndArray) {
                reader.Read();
                if (reader.TokenType == JsonToken.StartObject) {
                    var next = (TVal) converter.ReadJson(reader, typeof(TVal), null, serializer);
                    retVal.Add(next);
                }
            }

            return retVal;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
            throw new NotSupportedException();

        #endregion
    }

    public sealed class OutputTransformConverter : JsonConverter
    {
        #region Properties

        public override bool CanWrite => false;

        #endregion

        #region Overrides

        public override bool CanConvert(Type objectType) => typeof(IOutputTransform).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            var data = JToken.ReadFrom(reader);
            switch (Enum.Parse(typeof(MessagePortion), data["Portion"].Value<string>(), true)) {
                case MessagePortion.MessageNo:
                    return data.ToObject<MessageNumberOutputTransform>();
                case MessagePortion.Flags:
                    return data.ToObject<FlagsOutputTransform>();
                case MessagePortion.Profile:
                    return data.ToObject<ProfileOutputTransform>();
                case MessagePortion.Properties:
                    return data.ToObject<PropertiesOutputTransform>();
                case MessagePortion.Type:
                    return data.ToObject<TypeOutputTransform>();
                case MessagePortion.Body:
                    return data.ToObject<BodyOutputTransform>();
            }

            throw new InvalidDataException($"Unable to find class for portion {data["Portion"]}");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
            throw new NotSupportedException();

        #endregion
    }

    public enum MessagePortion
    {
        MessageNo,
        Flags,
        Profile,
        Type,
        Properties,
        Body
    }

    public interface IOutputTransform
    {
        #region Public Methods

        void Transform(ref BLIPMessage message);

        #endregion
    }

    public sealed class InputCriteria
    {
        #region Properties

        public Frequency ApplicationFrequency { get; set; } = Frequency.Always;

        public bool ApplyToReply { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public FrameFlags? Flags { get; set; }

        public ulong? Number { get; set; }

        public string Profile { get; set; }

        public IReadOnlyDictionary<string, string> Properties { get; set; }

        public MessageType? Type { get; set; }

        #endregion

        #region Public Methods

        public bool Matches(BLIPMessage message)
        {
            if (Type.HasValue && message.Type != Type) {
                return false;
            }

            if (Number.HasValue && Number != message.MessageNumber) {
                return false;
            }

            if (Profile != null && Profile != message.Profile) {
                return false;
            }

            if (Flags.HasValue && Flags != message.Flags) {
                return false;
            }

            if (Properties != null) {
                foreach (var entry in Properties) {
                    if (!message.Properties.Contains($"{entry.Key}:{entry.Value}")) {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            var sb = new StringBuilder();
            var prefix = "";
            if (Type.HasValue) {
                sb.AppendFormat("Type is {0}", Type.Value);
                prefix = " and ";
            }

            if (Number.HasValue) {
                sb.AppendFormat("{0}Number is {1}", prefix, Number.Value);
                prefix = " and ";
            }

            if (Profile != null) {
                sb.AppendFormat("{0}Profile is {1}", prefix, Profile);
                prefix = " and ";
            }

            if (Flags.HasValue) {
                sb.AppendFormat("{0}Flags is {1}", prefix, Flags.Value);
                prefix = " and ";
            }

            if (Properties != null) {
                sb.AppendFormat("{0}Properties contains {1}", prefix, JsonConvert.SerializeObject(Properties));
            }

            sb.AppendFormat(" (Frequency = {0})", ApplicationFrequency);
            return sb.ToString();
        }

        #endregion

        public enum Frequency
        {
            Always,
            OnlyOnce
        }
    }

    internal sealed class MessageNumberOutputTransform : IOutputTransform
    {
        #region Properties

        [JsonProperty(Required = Required.Always)]
        public ulong Number { get; }

        public Operation Op { get; }

        #endregion

        #region Constructors

        public MessageNumberOutputTransform(ulong number, Operation op = Operation.Replace)
        {
            Number = number;
            Op = op;
        }

        #endregion

        #region IOutputTransform

        public void Transform(ref BLIPMessage message)
        {
            switch (Op) {
                case Operation.Replace:
                    message.MessageNumber = Number;
                    break;
                case Operation.Add:
                    message.MessageNumber += Number;
                    break;
                case Operation.Subtract:
                    message.MessageNumber -= Number;
                    break;
                case Operation.Divide:
                    message.MessageNumber /= Number;
                    break;
                case Operation.Multiply:
                    message.MessageNumber *= Number;
                    break;
            }
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            switch (Op) {
                case Operation.Replace:
                    return $"Change MessageNumber to {Number}";
                case Operation.Add:
                    return $"Add {Number} to MessageNumber";
                case Operation.Subtract:
                    return $"Subtract {Number} from MessageNumber";
                case Operation.Multiply:
                    return $"Multiply MessageNumber by {Number}";
                case Operation.Divide:
                    return $"Divide MessageNumber by {Number}";
            }

            return "Error...";
        }

        #endregion

        public enum Operation
        {
            Replace,
            Add,
            Subtract,
            Multiply,
            Divide
        }
    }

    internal sealed class FlagsOutputTransform : IOutputTransform
    {
        #region Properties

        [JsonProperty(Required = Required.Always)]
        public FrameFlags Flags { get; }

        public Operation Op { get; }

        #endregion

        #region Constructors

        public FlagsOutputTransform(FrameFlags flags, Operation op = Operation.Replace)
        {
            Flags = flags;
            Op = op;
        }

        #endregion

        #region IOutputTransform

        public void Transform(ref BLIPMessage message)
        {
            switch (Op) {
                case Operation.Replace:
                    message.Flags = Flags;
                    break;
                case Operation.Add:
                    message.Flags |= Flags;
                    break;
                case Operation.Remove:
                    message.Flags &= ~Flags;
                    break;
            }
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            switch (Op) {
                case Operation.Replace:
                    return $"Replace Flags with {Flags}";
                case Operation.Add:
                    return $"Add {Flags} to Flags";
                case Operation.Remove:
                    return $"Remove {Flags} from Flags";
            }

            return "Error...";
        }

        #endregion

        public enum Operation
        {
            Replace,
            Add,
            Remove
        }
    }

    internal sealed class ProfileOutputTransform : IOutputTransform
    {
        #region Properties

        [JsonProperty(Required = Required.Always)]
        public string Profile { get; set; }

        #endregion

        #region Constructors

        public ProfileOutputTransform(string profile)
        {
            Profile = profile;
        }

        #endregion

        #region IOutputTransform

        public void Transform(ref BLIPMessage message)
        {
            var dictionary = new Dictionary<string, string>();
            string key = default;
            foreach (var entry in message.Properties.Split(':')) {
                if (key == null) {
                    key = entry;
                } else {
                    dictionary[key] = entry;
                    key = null;
                }
            }

            dictionary["Profile"] = Profile;
            message.Properties =
                dictionary.Aggregate(String.Empty, (x, y) => $"{x}:{y}", x => x);
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return $"Change Profile to {Profile}";
        }

        #endregion
    }

    internal sealed class TypeOutputTransform : IOutputTransform
    {
        #region Properties

        [JsonProperty(Required = Required.Always)]
        public MessageType Type { get; }

        #endregion

        #region Constructors

        public TypeOutputTransform(MessageType type)
        {
            Type = type;
        }

        #endregion

        #region IOutputTransform

        public void Transform(ref BLIPMessage message)
        {
            message.Type = Type;
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return $"Change Type to {Type}";
        }

        #endregion
    }

    internal sealed class PropertiesOutputTransform : IOutputTransform
    {
        #region Properties

        public Operation Op { get; }

        [JsonProperty(Required = Required.Always)]
        public IReadOnlyDictionary<string, string> Properties { get; }

        #endregion

        #region Constructors

        public PropertiesOutputTransform(IReadOnlyDictionary<string, string> properties,
            Operation op = Operation.Replace)
        {
            Properties = properties;
            Op = op;
        }

        #endregion

        #region IOutputTransform

        public void Transform(ref BLIPMessage message)
        {
            var dictionary = new Dictionary<string, string>();
            string key = default;
            foreach (var entry in message.Properties.Split(':')) {
                if (key == null) {
                    key = entry;
                } else {
                    dictionary[key] = entry;
                    key = null;
                }
            }

            switch (Op) {
                case Operation.Replace:
                    dictionary.Clear();
                    goto case Operation.Add;
                case Operation.Add:
                    foreach (var prop in Properties) {
                        dictionary[prop.Key] = prop.Value;
                    }

                    break;
                case Operation.Remove:
                    foreach (var prop in Properties) {
                        dictionary.Remove(prop.Key);
                    }

                    break;
            }

            if (dictionary.Count > 0) {
                var sb = new StringBuilder();
                var prefix = "";
                foreach (var entry in dictionary) {
                    sb.AppendFormat($"{prefix}{entry.Key}:{entry.Value}");
                    prefix = ":";
                }

                message.Properties = sb.ToString();
            }
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            switch (Op) {
                case Operation.Replace:
                    return $"Change Properties to {JsonConvert.SerializeObject(Properties)}";
                case Operation.Add:
                    return $"Add {JsonConvert.SerializeObject(Properties)} to Properties";
                case Operation.Remove:
                    return $"Remove {JsonConvert.SerializeObject(Properties)} from Properties";
            }

            return "Error...";
        }

        #endregion

        public enum Operation
        {
            Replace,
            Add,
            Remove
        }
    }

    internal sealed class BodyOutputTransform : IOutputTransform
    {
        #region Properties

        [JsonProperty(Required = Required.Always)]
        public string Content { get; }

        #endregion

        #region Constructors

        public BodyOutputTransform(string content)
        {
            Content = content;
        }

        #endregion

        #region IOutputTransform

        public void Transform(ref BLIPMessage message)
        {
            message.Body = Encoding.UTF8.GetBytes(Content);
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return $"Change Body to {Content}";
        }

        #endregion
    }
}