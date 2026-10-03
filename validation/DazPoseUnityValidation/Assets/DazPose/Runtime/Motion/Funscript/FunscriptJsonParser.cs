using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace DazPose.Motion
{
    /// <summary>Strict, small parser for the single-axis Funscript root actions format.</summary>
    public static class FunscriptJsonParser
    {
        public static FunscriptMotionProgram Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Funscript JSON is empty.");
            var reader = new Reader(json);
            ParsedDocument document = reader.ReadDocument();
            if (document.Actions == null || document.Actions.Count == 0)
                throw new FormatException("Funscript 'actions' must be a nonempty array.");
            if (document.Range <= 0)
                throw new FormatException("Funscript range must be a positive integer.");

            long previousTime = -1L;
            for (int i = 0; i < document.Actions.Count; i++)
            {
                FunscriptAction action = document.Actions[i];
                if (action.Position < 0 || action.Position > document.Range)
                    throw new FormatException("Funscript action " + i + " position is outside [0, range].");
                if (action.AtMilliseconds < previousTime)
                    throw new FormatException("Funscript action timestamps must be ordered; duplicate timestamps are preserved.");
                previousTime = action.AtMilliseconds;
            }

            var program = ScriptableObject.CreateInstance<FunscriptMotionProgram>();
            try
            {
                program.ConfigureImportedData(document.Actions.ToArray(), document.Version,
                    document.Inverted, document.Range, document.MetadataDurationSeconds,
                    document.Title, document.Description, document.Creator);
                return program;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(program);
                throw;
            }
        }

        private sealed class ParsedDocument
        {
            public List<FunscriptAction> Actions;
            public string Version = string.Empty;
            public bool Inverted;
            public int Range = 100;
            public double MetadataDurationSeconds;
            public string Title = string.Empty;
            public string Description = string.Empty;
            public string Creator = string.Empty;
        }

        private sealed class Reader
        {
            private readonly string _text;
            private int _index;

            public Reader(string text) { _text = text; }

            public ParsedDocument ReadDocument()
            {
                var document = new ParsedDocument();
                SkipWhitespace();
                Expect('{');
                SkipWhitespace();
                if (!TryConsume('}'))
                {
                    while (true)
                    {
                        string key = ReadString();
                        SkipWhitespace();
                        Expect(':');
                        SkipWhitespace();
                        switch (key)
                        {
                            case "actions": document.Actions = ReadActions(); break;
                            case "version": document.Version = ReadNullableString("version"); break;
                            case "inverted": document.Inverted = ReadBoolean(); break;
                            case "range": document.Range = ReadInt32("range"); break;
                            case "metadata": ReadMetadata(document); break;
                            default: SkipValue(); break;
                        }
                        SkipWhitespace();
                        if (TryConsume('}')) break;
                        Expect(',');
                        SkipWhitespace();
                    }
                }
                SkipWhitespace();
                if (_index != _text.Length) throw Error("Unexpected content after the root object.");
                return document;
            }

            private List<FunscriptAction> ReadActions()
            {
                var actions = new List<FunscriptAction>();
                Expect('[');
                SkipWhitespace();
                if (TryConsume(']')) return actions;
                while (true)
                {
                    actions.Add(ReadAction());
                    SkipWhitespace();
                    if (TryConsume(']')) return actions;
                    Expect(',');
                    SkipWhitespace();
                }
            }

            private FunscriptAction ReadAction()
            {
                Expect('{');
                SkipWhitespace();
                bool hasAt = false;
                bool hasPosition = false;
                long atMilliseconds = 0L;
                int position = 0;
                if (!TryConsume('}'))
                {
                    while (true)
                    {
                        string key = ReadString();
                        SkipWhitespace();
                        Expect(':');
                        SkipWhitespace();
                        if (key == "at")
                        {
                            if (hasAt) throw Error("An action contains duplicate 'at' fields.");
                            atMilliseconds = ReadInt64("action at");
                            hasAt = true;
                        }
                        else if (key == "pos")
                        {
                            if (hasPosition) throw Error("An action contains duplicate 'pos' fields.");
                            position = ReadInt32("action pos");
                            hasPosition = true;
                        }
                        else SkipValue();

                        SkipWhitespace();
                        if (TryConsume('}')) break;
                        Expect(',');
                        SkipWhitespace();
                    }
                }
                if (!hasAt || !hasPosition) throw Error("Every action must contain integer 'at' and 'pos' fields.");
                if (atMilliseconds < 0L) throw Error("Action timestamps cannot be negative.");
                return new FunscriptAction(atMilliseconds, position);
            }

            private void ReadMetadata(ParsedDocument document)
            {
                SkipWhitespace();
                if (TryReadNull()) return;
                Expect('{');
                SkipWhitespace();
                if (TryConsume('}')) return;
                while (true)
                {
                    string key = ReadString();
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    switch (key)
                    {
                        case "duration": document.MetadataDurationSeconds = ReadDouble("metadata.duration"); break;
                        case "title": document.Title = ReadNullableString("metadata.title"); break;
                        case "description": document.Description = ReadNullableString("metadata.description"); break;
                        case "creator": document.Creator = ReadNullableString("metadata.creator"); break;
                        default: SkipValue(); break;
                    }
                    SkipWhitespace();
                    if (TryConsume('}')) return;
                    Expect(',');
                    SkipWhitespace();
                }
            }

            private string ReadNullableString(string fieldName)
            {
                SkipWhitespace();
                if (TryReadNull()) return string.Empty;
                if (Peek() != '"') throw Error(fieldName + " must be a string or null.");
                return ReadString();
            }

            private bool ReadBoolean()
            {
                if (TryReadLiteral("true")) return true;
                if (TryReadLiteral("false")) return false;
                throw Error("Funscript 'inverted' must be a JSON boolean.");
            }

            private int ReadInt32(string fieldName)
            {
                string token = ReadNumberToken(fieldName);
                int value;
                if (!int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                    throw Error(fieldName + " must be an integer in the 32-bit range.");
                return value;
            }

            private long ReadInt64(string fieldName)
            {
                string token = ReadNumberToken(fieldName);
                long value;
                if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
                    throw Error(fieldName + " must be an integer in the 64-bit range.");
                return value;
            }

            private double ReadDouble(string fieldName)
            {
                string token = ReadNumberToken(fieldName);
                double value;
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    || double.IsNaN(value) || double.IsInfinity(value))
                    throw Error(fieldName + " must be finite numeric seconds.");
                if (value < 0d) throw Error(fieldName + " cannot be negative.");
                return value;
            }

            private string ReadNumberToken(string fieldName)
            {
                SkipWhitespace();
                int start = _index;
                if (TryConsume('-')) { }
                if (TryConsume('0'))
                {
                    if (IsDigit(Peek())) throw Error(fieldName + " has an invalid leading zero.");
                }
                else
                {
                    if (Peek() < '1' || Peek() > '9') throw Error(fieldName + " must be a JSON number.");
                    while (IsDigit(Peek())) _index++;
                }
                if (TryConsume('.'))
                {
                    if (!IsDigit(Peek())) throw Error(fieldName + " has an invalid fractional part.");
                    while (IsDigit(Peek())) _index++;
                }
                if (Peek() == 'e' || Peek() == 'E')
                {
                    _index++;
                    if (Peek() == '+' || Peek() == '-') _index++;
                    if (!IsDigit(Peek())) throw Error(fieldName + " has an invalid exponent.");
                    while (IsDigit(Peek())) _index++;
                }
                return _text.Substring(start, _index - start);
            }

            private string ReadString()
            {
                SkipWhitespace();
                Expect('"');
                var builder = new StringBuilder();
                while (_index < _text.Length)
                {
                    char current = _text[_index++];
                    if (current == '"') return builder.ToString();
                    if (current < 0x20) throw Error("Unescaped control character in JSON string.");
                    if (current != '\\')
                    {
                        builder.Append(current);
                        continue;
                    }
                    if (_index >= _text.Length) throw Error("Incomplete escape in JSON string.");
                    char escape = _text[_index++];
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u': builder.Append(ReadUnicodeEscape()); break;
                        default: throw Error("Invalid JSON string escape '\\" + escape + "'.");
                    }
                }
                throw Error("Unterminated JSON string.");
            }

            private char ReadUnicodeEscape()
            {
                if (_index + 4 > _text.Length) throw Error("Incomplete Unicode escape in JSON string.");
                int value = 0;
                for (int i = 0; i < 4; i++)
                {
                    char digit = _text[_index++];
                    int hex = digit >= '0' && digit <= '9' ? digit - '0'
                        : digit >= 'a' && digit <= 'f' ? digit - 'a' + 10
                        : digit >= 'A' && digit <= 'F' ? digit - 'A' + 10 : -1;
                    if (hex < 0) throw Error("Invalid Unicode escape in JSON string.");
                    value = (value * 16) + hex;
                }
                return (char)value;
            }

            private void SkipValue()
            {
                SkipWhitespace();
                char value = Peek();
                if (value == '{') { SkipObject(); return; }
                if (value == '[') { SkipArray(); return; }
                if (value == '"') { ReadString(); return; }
                if (TryReadLiteral("true") || TryReadLiteral("false") || TryReadNull()) return;
                ReadNumberToken("value");
            }

            private void SkipObject()
            {
                Expect('{');
                SkipWhitespace();
                if (TryConsume('}')) return;
                while (true)
                {
                    ReadString();
                    SkipWhitespace();
                    Expect(':');
                    SkipValue();
                    SkipWhitespace();
                    if (TryConsume('}')) return;
                    Expect(',');
                    SkipWhitespace();
                }
            }

            private void SkipArray()
            {
                Expect('[');
                SkipWhitespace();
                if (TryConsume(']')) return;
                while (true)
                {
                    SkipValue();
                    SkipWhitespace();
                    if (TryConsume(']')) return;
                    Expect(',');
                    SkipWhitespace();
                }
            }

            private bool TryReadNull() => TryReadLiteral("null");

            private bool TryReadLiteral(string literal)
            {
                SkipWhitespace();
                if (_index + literal.Length > _text.Length
                    || string.CompareOrdinal(_text, _index, literal, 0, literal.Length) != 0)
                    return false;
                _index += literal.Length;
                return true;
            }

            private void SkipWhitespace()
            {
                while (_index < _text.Length)
                {
                    char value = _text[_index];
                    if (value == ' ' || value == '\t' || value == '\r' || value == '\n' || value == '\uFEFF') _index++;
                    else return;
                }
            }

            private bool TryConsume(char expected)
            {
                if (Peek() != expected) return false;
                _index++;
                return true;
            }

            private void Expect(char expected)
            {
                if (!TryConsume(expected)) throw Error("Expected '" + expected + "'.");
            }

            private char Peek() => _index < _text.Length ? _text[_index] : '\0';

            private FormatException Error(string message) =>
                new FormatException(message + " (character " + _index + ").");

            private static bool IsDigit(char value) => value >= '0' && value <= '9';
        }
    }
}
