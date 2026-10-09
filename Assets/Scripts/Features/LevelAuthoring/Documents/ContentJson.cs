#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LevelAuthoring.Validation;

namespace LevelAuthoring.Documents
{
    public static class ContentJson
    {
        public static ContentDocument Read(string text)
        {
            try
            {
                ValidateSyntax(text);
                using (var input = new StringReader(text ?? throw new ContentFormatException("JSON 내용이 없습니다.")))
                using (var reader = new StrictReader(input))
                {
                    var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new ContentFormatException("문서 뒤에 추가 JSON이 있습니다.");
                    string[] names = { "kind", "schemaVersion", "id", "data" };
                    if (root.Properties().Count() != names.Length || names.Any(name => root.Property(name) == null))
                        throw new ContentFormatException("문서에는 kind/schemaVersion/id/data만 필요합니다.");
                    if (root["schemaVersion"].Type != JTokenType.Integer || root["schemaVersion"].Value<long>() != 1)
                        throw new ContentFormatException("지원하지 않는 schemaVersion입니다. 지원 버전: 1.");
                    if (root["kind"].Type != JTokenType.String || root["id"].Type != JTokenType.String || !(root["data"] is JObject data))
                        throw new ContentFormatException("문서 kind/id/data 형식이 잘못됐습니다.");
                    var result = new ContentDocument((string)root["kind"], (string)root["id"], data);
                    ContentDocumentValidator.Validate(result);
                    return result;
                }
            }
            catch (ContentFormatException) { throw; }
            catch (Exception error) when (error is JsonException || error is OverflowException || error is FormatException)
            { throw new ContentFormatException("JSON 읽기 오류: " + error.Message, error); }
        }

        public static string Write(ContentDocument document)
        {
            ContentDocumentValidator.Validate(document);
            var root = new JObject { ["kind"] = document.Kind, ["schemaVersion"] = 1, ["id"] = document.Id, ["data"] = Canonical(document.Data) };
            using (var text = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" })
            using (var writer = new JsonTextWriter(text) { Formatting = Formatting.Indented, Indentation = 2, IndentChar = ' ' })
            {
                root.WriteTo(writer);
                writer.Flush();
                return text + "\n";
            }
        }

        private static JToken Canonical(JToken value)
        {
            if (value is JObject obj) return new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new JProperty(p.Name, Canonical(p.Value))));
            if (value is JArray array) return new JArray(array.Select(Canonical));
            return value.DeepClone();
        }

        // Newtonsoft의 JavaScript 확장(끝 쉼표/16진수 등)을 외부 JSON 계약에 허용하지 않는다.
        private static void ValidateSyntax(string text)
        {
            if (text == null) throw new ContentFormatException("JSON 내용이 없습니다.");
            char previous = '\0';
            for (int index = 0; index < text.Length; index++)
            {
                char current = text[index];
                if (" \t\r\n".IndexOf(current) >= 0) continue;
                if (current == '"')
                {
                    bool closed = false;
                    while (++index < text.Length)
                    {
                        if (text[index] < 32) throw new ContentFormatException("문자열의 제어문자는 이스케이프해야 합니다.");
                        if (text[index] == '\\') { index++; continue; }
                        if (text[index] == '"') { closed = true; break; }
                    }
                    if (!closed) throw new ContentFormatException("문자열이 닫히지 않았습니다.");
                    previous = '"'; continue;
                }
                if ("{}[]:,".IndexOf(current) >= 0)
                {
                    if ((current == '}' || current == ']') && previous == ',') throw new ContentFormatException("끝 쉼표는 허용하지 않습니다.");
                    previous = current; continue;
                }
                int start = index;
                while (index + 1 < text.Length && " \t\r\n{}[]:,".IndexOf(text[index + 1]) < 0) index++;
                string word = text.Substring(start, index - start + 1);
                if (word != "true" && word != "false" && word != "null" &&
                    !Regex.IsMatch(word, @"\A-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?\z", RegexOptions.CultureInvariant))
                    throw new ContentFormatException("표준 JSON 값이 아닙니다: " + word);
                previous = 'v';
            }
        }

        private sealed class StrictReader : JsonTextReader
        {
            public StrictReader(TextReader reader) : base(reader) { DateParseHandling = DateParseHandling.None; MaxDepth = 96; }
            public override bool Read()
            {
                bool result = base.Read();
                if (result && (TokenType == JsonToken.Comment || TokenType == JsonToken.Undefined || TokenType == JsonToken.StartConstructor ||
                    ((TokenType == JsonToken.String || TokenType == JsonToken.PropertyName) && QuoteChar != '"')))
                    throw new ContentFormatException("표준 JSON 문법만 사용할 수 있습니다.");
                return result;
            }
        }
    }
}
#endif
