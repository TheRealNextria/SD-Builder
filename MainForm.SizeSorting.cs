// Auto-added partial: Size column sorting (safe patch)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SDBuilderWin
{
    public sealed partial class MainForm
    {
        private class SizeColumnComparer : IComparer
        {
            private readonly int _column;
            private readonly SortOrder _order;
            public SizeColumnComparer(int column, SortOrder order) { _column = column; _order = order; }

            public int Compare(object x, object y)
        {
            var a = x as ListViewItem;
            var b = y as ListViewItem;
            if (a == null || b == null) return 0;

            string sa = _column < a.SubItems.Count ? a.SubItems[_column].Text : string.Empty;
            string sb = _column < b.SubItems.Count ? b.SubItems[_column].Text : string.Empty;

            // Always-top rule for blanks / "Installed" / dashes
            bool aZero = IsZeroLike(sa);
            bool bZero = IsZeroLike(sb);
            if (aZero || bZero)
            {
                if (aZero && !bZero) return -1; // A before B
                if (!aZero && bZero) return 1;  // B before A
                // both zero-like: stable secondary by item text (name)
                return string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);
            }

            // Numeric sort for real sizes
            double da = ParseSizeToBytes(sa);
            double db = ParseSizeToBytes(sb);

            int result = (!double.IsNaN(da) && !double.IsNaN(db))
                ? da.CompareTo(db)
                : string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);

            return _order == SortOrder.Descending ? -result : result;
        }
        }

        private static double ParseSizeToBytes(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return double.NaN;
            s = s.Trim();

            var m = System.Text.RegularExpressions.Regex.Match(
                s, @"^\s*([0-9]+(?:[.,][0-9]+)?)\s*([KMG]?B)\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (m.Success)
            {
                var numStr = m.Groups[1].Value.Replace(',', '.');
                if (!double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
                    return double.NaN;

                switch (m.Groups[2].Value.ToUpperInvariant())
                {
                    case "KB": value *= 1024.0; break;
                    case "MB": value *= 1024.0 * 1024.0; break;
                    case "GB": value *= 1024.0 * 1024.0 * 1024.0; break;
                }
                return value;
            }

            if (double.TryParse(s.Replace(",", "").Replace(" ", ""), out var plain)) return plain;
            return double.NaN;
        }

        
        private static bool IsZeroLike(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return true;
            var t = s.Trim();
            if (t.Equals("Installed", StringComparison.OrdinalIgnoreCase)) return true;
            if (t == "-" || t == "—") return true;
            // Also treat numeric zero as zero-like (e.g., "0", "0 B")
            var v = ParseSizeToBytes(t);
            if (!double.IsNaN(v) && Math.Abs(v) < 0.5) return true;
            return false;
        }

        
        private static string StripSortGlyph(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(s, @"\s*[▲▼]\s*$", "");
        }

        
        private static void UpdateHeaderGlyph(ListView lv, int col, SortOrder order)
        {
            for (int i = 0; i < lv.Columns.Count; i++)
            {
                var baseText = StripSortGlyph(lv.Columns[i].Text ?? string.Empty).TrimEnd();
                if (i == col)
                {
                    string glyph = (order == SortOrder.Descending) ? "▼" : "▲";
                    lv.Columns[i].Text = string.IsNullOrEmpty(baseText) ? glyph : (baseText + " " + glyph);
                }
                else
                {
                    lv.Columns[i].Text = baseText;
                }
            }
        }

        // Remember per-ListView sort order
        private readonly Dictionary<ListView, SortOrder> _sizeSortOrders = new Dictionary<ListView, SortOrder>();

        // One handler for all lists; only sorts column index 1 (Size)
        private void OnListViewColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (sender is not ListView lv) return;
            lv.BeginUpdate();
            try
            {

            // Determine Size column: prefer header text "Size" (ignoring ▲/▼), else assume index 1
            int sizeCol = -1;
            for (int i = 0; i < lv.Columns.Count; i++)
            {
                var header = lv.Columns[i].Text;
                var baseHeader = StripSortGlyph(header ?? string.Empty).Trim();
                if (string.Equals(baseHeader, "Size", StringComparison.OrdinalIgnoreCase))
                {
                    sizeCol = i;
                    break;
                }
            }
            if (sizeCol == -1) sizeCol = (e.Column == 1 ? 1 : -1);
            if (sizeCol == -1 || e.Column != sizeCol) return;

            // Toggle per-ListView order
            _sizeSortOrders.TryGetValue(lv, out var order);
            order = (order == SortOrder.Ascending) ? SortOrder.Descending : SortOrder.Ascending;
            _sizeSortOrders[lv] = order;

            // Use custom comparer (disable built-in Sorting)
            lv.Sorting = SortOrder.None;
            lv.ShowGroups = false;
            lv.ListViewItemSorter = new SizeColumnComparer(sizeCol, order);
            lv.Sort();

            // Update header glyph (▲ ascending, ▼ descending)
            UpdateHeaderGlyph(lv, sizeCol, order);
            }
            finally { lv.EndUpdate(); }
        }
}
}
