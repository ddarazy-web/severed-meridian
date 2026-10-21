#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LevelAuthoring.Editing
{
    // 편집 문서를 복제하지 않고 목록에 필요한 정보만 보관한다.
    public sealed class LevelBrowserIndex
    {
        public sealed class Entry
        {
            public string Id { get; }
            public int Number { get; }
            public string Name { get; }
            public Entry(string id, int number, string name) { Id = id; Number = number; Name = name ?? ""; }
        }

        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private Dictionary<int, IReadOnlyList<Entry>> numbers = new Dictionary<int, IReadOnlyList<Entry>>();
        private IReadOnlyList<Entry> sorted = Array.Empty<Entry>();
        private bool changed;
        public int Count => entries.Count;
        public long Revision { get; private set; }
        public int SortBuildCount { get; private set; }
        public string SearchError { get; private set; }

        public bool Upsert(string id, int number, string name)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("레벨 ID가 필요합니다.", nameof(id));
            name ??= "";
            if (entries.TryGetValue(id, out Entry existing) && existing.Number == number && existing.Name == name) return false;
            entries[id] = new Entry(id, number, name);
            changed = true; Revision++;
            return true;
        }

        public bool Remove(string id)
        {
            if (!entries.Remove(id)) return false;
            changed = true; Revision++;
            return true;
        }

        public Entry Get(string id) => id != null && entries.TryGetValue(id, out Entry entry) ? entry : null;

        public IReadOnlyList<Entry> Search(string query)
        {
            if (changed)
            {
                Entry[] ordered = entries.Values.OrderBy(entry => entry.Number).ThenBy(entry => entry.Id, StringComparer.Ordinal).ToArray();
                sorted = Array.AsReadOnly(ordered);
                numbers = ordered.GroupBy(entry => entry.Number).ToDictionary(group => group.Key,
                    group => (IReadOnlyList<Entry>)Array.AsReadOnly(group.ToArray()));
                changed = false; SortBuildCount++;
            }
            SearchError = null;
            query = (query ?? "").Trim();
            if (query.Length == 0) return sorted;
            if (query.Contains("~"))
            {
                string[] bounds = query.Split('~');
                if (bounds.Length != 2 || !int.TryParse(bounds[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int first) ||
                    !int.TryParse(bounds[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int last) || first < 1 || first > last)
                {
                    SearchError = "범위는 1200~1299처럼 작은 번호부터 입력하세요.";
                    return Array.Empty<Entry>();
                }
                int low = 0, high = sorted.Count;
                while (low < high)
                {
                    int middle = low + (high - low) / 2;
                    if (sorted[middle].Number < first) low = middle + 1;
                    else high = middle;
                }
                var found = new List<Entry>();
                for (int i = low; i < sorted.Count && sorted[i].Number <= last; i++) found.Add(sorted[i]);
                return found.AsReadOnly();
            }
            if (query.All(char.IsDigit))
            {
                if (!int.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number < 1)
                {
                    SearchError = "레벨 번호는 1 이상의 유효한 정수로 입력하세요.";
                    return Array.Empty<Entry>();
                }
                return numbers.TryGetValue(number, out IReadOnlyList<Entry> matches) ? matches : Array.Empty<Entry>();
            }
            return Array.AsReadOnly(sorted.Where(entry => entry.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray());
        }
    }
}
#endif
