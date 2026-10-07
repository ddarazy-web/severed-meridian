using System;
using System.Collections.Generic;

namespace Elements.Editor
{
    // 기존 원문과 비교할 때 이번에 추가된 순수 표시 이력만 분리한다. 다른 필드는 그대로 비교한다.
    internal static class RecordedLogicComparison
    {
        internal static bool Equal(IEnumerable<string> baseline, IEnumerable<string> current)
        {
            using IEnumerator<string> before = baseline.GetEnumerator();
            using IEnumerator<string> after = current.GetEnumerator();
            while (before.MoveNext())
            {
                if (!after.MoveNext()) return false;
                if (before.Current == after.Current) continue;
                string canonical = WithoutDefaultAuthoringMetadata(after.Current);
                if (before.Current == canonical) continue;
                if (before.Current.Contains("|Flights=[") || before.Current != WithoutAddedFlights(canonical)) return false;
            }
            return !after.MoveNext();
        }

        // 구형 레벨 JSON 뒤에 추가된 빈 제작 필드만 분리한다. 원래 필드/신규 값은 변경하지 않는다.
        internal static string WithoutDefaultAuthoringMetadata(string value)
        {
            string suffix = ",\"elements\":[],\"elementCatalog\":{\"instanceID\":0},\"elementSupply\":{\"sources\":[],\"scrapTarget\":0,\"scrapLimit\":0,\"scrapDurability\":1,\"recoveryTarget\":0,\"scrapDefinitionId\":\"supply.scrap\",\"recoveryDefinitionId\":\"supply.recovery\"},\"embeddedDefinitions\":[]";
            // 기존 관찰은 JSON 문자열을 최대 두 번 중첩해 기록한다.
            for (int depth = 0; depth <= 2; depth++)
            {
                value = value.Replace(suffix, "");
                suffix = suffix.Replace("\\", "\\\\").Replace("\"", "\\\"");
            }
            return value;
        }

        private static string WithoutAddedFlights(string value)
        {
            int start = value.IndexOf("|Flights=[", StringComparison.Ordinal);
            while (start >= 0)
            {
                int cursor = start + 9, depth = 1;
                while (depth > 0 && ++cursor < value.Length)
                {
                    if (value[cursor] == '[') depth++;
                    else if (value[cursor] == ']') depth--;
                }
                if (depth != 0) throw new InvalidOperationException("추가 비행 표시 이력 형식 불완전");
                value = value.Remove(start, cursor - start + 1);
                start = value.IndexOf("|Flights=[", StringComparison.Ordinal);
            }
            return value;
        }
    }
}
