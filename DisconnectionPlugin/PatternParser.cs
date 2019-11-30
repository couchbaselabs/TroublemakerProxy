﻿// 
// PatternParser.cs
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
using System.Runtime.CompilerServices;

using sly.lexer;
using sly.parser.generator;

using TroublemakerInterfaces;

namespace DisconnectionPlugin
{
    public enum PatternToken
    {
        [Lexeme(GenericToken.Int)]
        Integer,

        
        
        [Lexeme(GenericToken.Identifier)]
        Identifier,
        /*
        [Lexeme(GenericToken.SugarToken, ">")]
        GreaterThan,

        [Lexeme(GenericToken.SugarToken, "<")]
        LessThan,

        [Lexeme(GenericToken.SugarToken, ">=")]
        GreaterThanOrEqual,

        [Lexeme(GenericToken.SugarToken, "<=")]
        LessThanOrEqual,
        */

        [Lexeme(GenericToken.SugarToken, "=")]
        [Lexeme(GenericToken.SugarToken, "==")]
        Equal,

        /*
        [Lexeme(GenericToken.SugarToken, "!=")]
        NotEqual,

        [Lexeme(GenericToken.KeyWord, "BETWEEN")]
        Between,

        [Lexeme(GenericToken.KeyWord, "AND")]
        And,
        */

        [Lexeme(GenericToken.KeyWord, "after")]
        After,

        [Lexeme(GenericToken.KeyWord, "before")]
        Before,

        [Lexeme(GenericToken.KeyWord, "minutes")]
        [Lexeme(GenericToken.KeyWord, "minute")]
        Minutes,

        [Lexeme(GenericToken.KeyWord, "seconds")]
        [Lexeme(GenericToken.KeyWord, "second")]
        Seconds,

        [Lexeme(GenericToken.KeyWord, "milliseconds")]
        [Lexeme(GenericToken.KeyWord, "millisecond")]
        Milliseconds,

        [Lexeme(GenericToken.KeyWord, "request")]
        [Lexeme(GenericToken.KeyWord, "msg")]
        BlipTypeRequest,

        [Lexeme(GenericToken.KeyWord, "response")]
        [Lexeme(GenericToken.KeyWord, "rpy")]
        BlipTypeResponse,

        [Lexeme(GenericToken.KeyWord, "error")]
        [Lexeme(GenericToken.KeyWord, "err")]
        BlipTypeError,

        [Lexeme(GenericToken.KeyWord, "type")]
        BlipType,

        [Lexeme(GenericToken.KeyWord, "msgno")]
        [Lexeme(GenericToken.KeyWord, "num")]
        BlipMsgNo
    }

    internal sealed class PatternParser
    {
        private readonly Pattern _result = new Pattern();

        [Production("time: Integer time_part")]
        public Pattern Time(Token<PatternToken> value, Pattern timePart)
        {
            switch ((PatternToken)timePart.Aggregate) {
                case PatternToken.Minutes:
                    timePart.Aggregate = TimeSpan.FromMinutes(value.IntValue);
                    break;
                case PatternToken.Seconds:
                    timePart.Aggregate = TimeSpan.FromSeconds(value.IntValue);
                    break;
                case PatternToken.Milliseconds:
                    timePart.Aggregate = TimeSpan.FromMilliseconds(value.IntValue);
                    break;
                default:
                    throw new ParserConfigurationException("Invalid time type!");
            }

            return _result;
        }

        [Production("blip_msg_type: BlipTypeRequest")]
        [Production("blip_msg_type: BlipTypeResponse")]
        [Production("blip_msg_type: BlipTypeError")]
        [Production("time_part: Minutes")]
        [Production("time_part: Seconds")]
        [Production("time_part: Milliseconds")]
        public Pattern TokenPart(Token<PatternToken> type)
        {
            _result.Aggregate = type.TokenID;
            return _result;
        }

        [Production("blip_comparison: Before time")]
        [Production("blip_comparison: After time")]
        public Pattern TimeOp(Token<PatternToken> type, Pattern aggregate)
        {
            var time = (TimeSpan) aggregate.Aggregate;
            if (type.TokenID == PatternToken.Before) {
                _result.AddClause((msg, timeInput) => timeInput < time);
            } else {
                _result.AddClause((msg, timeInput) => timeInput > time);
            }
            return _result;
        }

        [Production("blip_comparison: BlipMsgNo Equal Integer")]
        public Pattern BlipMsgNoComparison(Token<PatternToken> msgNoToken, Token<PatternToken> equal, Token<PatternToken> value)
        {
            var val = value.IntValue;
            _result.AddClause((msg, timeInput) => msg.MessageNumber == (ulong)val);
            return _result;
        }

        [Production("blip_comparison: BlipType Equal blip_msg_type")]
        public Pattern BlipTypeComparison(Token<PatternToken> msgTypeToken, Token<PatternToken> equal,
            Pattern type)
        {
            var typeValue = (PatternToken) type.Aggregate;
            _result.AddClause((msg, timeInput) =>
            {
                switch (typeValue) {
                    case PatternToken.BlipTypeRequest:
                        return msg.Type == MessageType.Request;
                    case PatternToken.BlipTypeResponse:
                        return msg.Type == MessageType.Response;
                    case PatternToken.BlipTypeError:
                        return msg.Type == MessageType.Error;
                }

                return false;
            });

            return _result;
        }
    }

    internal sealed class Pattern
    {
        private readonly List<Func<BLIPMessage, TimeSpan, bool>> _clauses = new List<Func<BLIPMessage, TimeSpan, bool>>();

        public object Aggregate { get; set; }

        public void AddClause(Func<BLIPMessage,  TimeSpan,bool> clause) => _clauses.Add(clause);

        public bool Evaluate(BLIPMessage msg, TimeSpan elapsed)
        {
            foreach (var clause in _clauses) {
                if (!clause(msg, elapsed)) {
                    return false;
                }
            }

            return true;
        }
    }
}